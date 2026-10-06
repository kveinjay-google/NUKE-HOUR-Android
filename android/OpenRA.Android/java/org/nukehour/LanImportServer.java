package org.nukehour;

import fi.iki.elonen.NanoHTTPD;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.security.SecureRandom;
import java.util.*;
import java.util.concurrent.*;

/** App-owned, bounded LAN intake; never serves the device filesystem. */
public final class LanImportServer extends NanoHTTPD {
    static LanImportServer instance;
    final File directory;
    final String html, host, token;
    final Map<String, Upload> uploads = new LinkedHashMap<>();
    volatile String state = "receiving", message = "";
    volatile boolean claimed;
    volatile long receivedBytes;
    volatile long touched = System.nanoTime();
    final Set<ClientHandler> clients = ConcurrentHashMap.newKeySet();
    final ThreadPoolExecutor workers = new ThreadPoolExecutor(4, 4, 30, TimeUnit.SECONDS,
        new ArrayBlockingQueue<Runnable>(8));
    static final long MAX_TOTAL = 8L * 1024 * 1024 * 1024;
    static final int CHUNK = 4 * 1024 * 1024;
    static final class Upload {
        final File file; final long size; final String modified;
        Upload(File f, long s, String m) { file=f; size=s; modified=m; }
    }
    LanImportServer(String host, File dir, String html) throws IOException {
        super(host, 0); this.host=host; this.directory=dir; this.html=html;
        if (!dir.mkdirs() && !dir.isDirectory()) throw new IOException("Cannot create intake directory");
        SecureRandom random = new SecureRandom();
        byte[] secret = new byte[32]; random.nextBytes(secret);
        StringBuilder text = new StringBuilder();
        for (byte b : secret) text.append(String.format(Locale.ROOT, "%02x", b & 255));
        token = text.toString();
        setAsyncRunner(new AsyncRunner() {
            public void exec(ClientHandler handler) {
                clients.add(handler);
                try { workers.execute(handler); }
                catch (RejectedExecutionException e) { clients.remove(handler); handler.close(); }
            }
            public void closed(ClientHandler handler) { clients.remove(handler); }
            public void closeAll() {
                for (ClientHandler handler : clients) handler.close();
                clients.clear(); workers.shutdownNow();
            }
        });
        start(30000, true);
    }
    public static synchronized String startServer(String host, String directory, String html) throws IOException {
        stopServer(); instance = new LanImportServer(host, new File(directory), html);
        return "http://" + host + ":" + instance.getListeningPort() + "/#" + instance.token;
    }
    public static synchronized void stopServer() {
        if (instance != null) { instance.stop(); instance = null; }
    }
    public static synchronized String takeImport() {
        if (instance == null) return "";
        if (System.nanoTime() - instance.touched > TimeUnit.MINUTES.toNanos(15) && !instance.state.equals("importing")) {
            stopServer(); return "expired";
        }
        if (!instance.state.equals("queued") || instance.claimed) return "";
        instance.claimed = true; instance.state = "importing";
        return instance.directory.getAbsolutePath();
    }
    public static synchronized void complete(String message) {
        if (instance != null) {
            instance.state = message.isEmpty() ? "complete" : "failed";
            instance.message = message; instance.touched = System.nanoTime();
        }
    }
    public static synchronized String summary() {
        return instance == null ? "closed" : Long.toString(instance.receivedBytes);
    }
    synchronized String status() {
        long done=0,total=0;
        for (Upload u : uploads.values()) { done += u.file.length(); total += u.size; }
        return "{\"state\":\""+state+"\",\"received\":"+done+",\"total\":"+total+
            ",\"files\":"+uploads.size()+",\"message\":\""+escape(message)+"\"}";
    }
    static String escape(String s) { return s.replace("\\", "\\\\").replace("\"", "\\\"").replace("\n", "\\n").replace("\r", " "); }
    Response reply(Response.Status status, String body) {
        Response r = newFixedLengthResponse(status, "application/json; charset=utf-8", body);
        r.addHeader("Cache-Control", "no-store"); r.addHeader("X-Content-Type-Options", "nosniff");
        r.closeConnection(true); return r;
    }
    Response error(String message) { return reply(Response.Status.BAD_REQUEST,"{\"error\":\""+escape(message)+"\"}"); }
    long length(IHTTPSession s) { return Long.parseLong(s.getHeaders().getOrDefault("content-length", "0")); }
    @Override public synchronized Response serve(IHTTPSession session) {
        try {
            String authority = host+":"+getListeningPort();
            if (!authority.equals(session.getHeaders().get("host"))) return error("Invalid host");
            String origin = session.getHeaders().get("origin");
            if (origin != null && !("http://"+authority).equals(origin)) return error("Invalid origin");
            String path = session.getUri();
            if (path.equals("/") && session.getMethod()==Method.GET) {
                Response r = newFixedLengthResponse(Response.Status.OK,"text/html; charset=utf-8",html);
                r.addHeader("Cache-Control","no-store");
                r.addHeader("Referrer-Policy","no-referrer");
                r.addHeader("Content-Security-Policy","default-src 'self'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; frame-ancestors 'none'");
                r.closeConnection(true); return r;
            }
            if (!token.equals(session.getHeaders().get("x-import-token")))
                return reply(Response.Status.UNAUTHORIZED,"{\"error\":\"Open the complete link copied from the phone\"}");
            touched=System.nanoTime();
            if (path.equals("/status") && session.getMethod()==Method.GET) return reply(Response.Status.OK,status());
            if (!state.equals("receiving")) return error("Session is already importing or finished");
            if (path.equals("/file") && session.getMethod()==Method.POST) {
                String name=session.getParms().getOrDefault("name","").toLowerCase(Locale.ROOT);
                if (name.length()>128 || name.startsWith(".") || name.contains("..") || !name.matches("[a-z0-9 _().-]+\\.(mix|aud|bag|idx|map|mpr|oramap|wav|yrm|zip)")) return error("Unsupported filename");
                long size=Long.parseLong(session.getParms().getOrDefault("size","-1"));
                String modified=session.getParms().getOrDefault("modified","");
                if (size<=0 || size>MAX_TOTAL || modified.length()>32) return error("Invalid file size");
                Upload upload=uploads.get(name);
                if (upload==null) {
                    long total=size; for(Upload u:uploads.values())total+=u.size;
                    if(uploads.size()>=64 || total>MAX_TOTAL || directory.getUsableSpace()<total*2+67108864) return error("Insufficient storage or session limit");
                    upload=new Upload(new File(directory,name),size,modified); uploads.put(name,upload);
                }
                if(upload.size!=size || !upload.modified.equals(modified))return error("File changed; start a new phone session");
                return reply(Response.Status.OK,"{\"offset\":"+upload.file.length()+"}");
            }
            if(path.equals("/chunk") && session.getMethod()==Method.PUT) {
                Upload upload=uploads.get(session.getParms().getOrDefault("name","").toLowerCase(Locale.ROOT));
                long offset=Long.parseLong(session.getParms().getOrDefault("offset","-1")), size=length(session);
                if(upload==null || size<=0 || size>CHUNK || offset!=upload.file.length() || size>upload.size-offset) return error("Invalid chunk offset or size; retry upload");
                try(RandomAccessFile out=new RandomAccessFile(upload.file,"rw")) {
                    out.seek(offset); byte[] buffer=new byte[65536]; long left=size;
                    try {
                        while(left>0) { int read=session.getInputStream().read(buffer,0,(int)Math.min(left,buffer.length)); if(read<0)throw new EOFException(); out.write(buffer,0,read);left-=read; }
                    } catch(IOException e) { out.setLength(offset); throw e; }
                }
                receivedBytes += size;
                return reply(Response.Status.OK,"{\"offset\":"+upload.file.length()+"}");
            }
            if(path.equals("/commit") && session.getMethod()==Method.POST) {
                if(uploads.isEmpty())return error("No files uploaded");
                for(Upload u:uploads.values())if(u.file.length()!=u.size)return error("Upload is incomplete");
                state="queued";return reply(Response.Status.OK,status());
            }
            return error("Unknown request");
        }catch(Exception e){return error("Request failed: "+e.getClass().getSimpleName());}
    }
    // Standalone bounded integration probe, not invoked by the Android host.
    public static void main(String[] args) throws Exception {
        System.out.println(startServer("127.0.0.1",args[0],"LAN import test"));
        System.out.flush(); System.in.read(); stopServer();
    }
}
