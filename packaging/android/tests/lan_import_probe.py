#!/usr/bin/env python3
"""Bounded real HTTP protocol smoke probe for the Android Java intake server."""
import urllib.parse
import json, subprocess, tempfile, urllib.request, urllib.error, pathlib, time, threading, os, signal
JAVA='/opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home/bin/java'
with tempfile.TemporaryDirectory(prefix='nukehour-lan-test-') as staging:
    p=subprocess.Popen([JAVA,'-XX:ActiveProcessorCount=4','-Xmx128m','-cp','/tmp/nukehour-lan-probe','org.nukehour.LanImportServer',staging],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,start_new_session=True)
    def timeout():
        if p.poll() is None: os.killpg(p.pid,signal.SIGKILL)
    watchdog=threading.Timer(45,timeout);watchdog.start()
    try:
        link=p.stdout.readline().strip(); parts=urllib.parse.urlsplit(link); token=parts.fragment
        assert len(token)==64 and all(c in '0123456789abcdef' for c in token), 'Full access link must carry a random credential'
        url=urllib.parse.urlunsplit(parts._replace(fragment=''));assert url.startswith('http://127.0.0.1:')
        def call(path,method='GET',body=None,token=None,headers=None):
            h=headers or {}
            if token:h['X-Import-Token']=token
            try:
                with urllib.request.urlopen(urllib.request.Request(url.rstrip('/')+path,data=body,headers=h,method=method),timeout=4) as r:return r.status,json.load(r)
            except urllib.error.HTTPError as e:return e.code,json.load(e)
        assert call('/status')[0]==401
        assert call('/status',token='0'*64)[0]==401
        assert call('/pair','POST',b'123456')[0]==401
        assert call('/status',token=token)[0]==200
        assert call('/status',token=token,headers={'Origin':'http://attacker.invalid'})[0]==400
        assert call('/file?name=../ra2.mix&size=8','POST',b'',token)[0]==400
        assert call('/file?name=ra2.exe&size=8','POST',b'',token)[0]==400
        assert call('/file?name=ra2.mix&size=8&modified=1','POST',b'',token)[1]['offset']==0
        assert call('/chunk?name=ra2.mix&offset=0','PUT',b'1234',token)[1]['offset']==4
        assert call('/commit','POST',b'',token)[0]==400
        assert call('/file?name=ra2.mix&size=8&modified=1','POST',b'',token)[1]['offset']==4
        assert call('/file?name=ra2.mix&size=8&modified=2','POST',b'',token)[0]==400
        assert call('/chunk?name=ra2.mix&offset=0','PUT',b'1234',token)[0]==400
        assert call('/chunk?name=ra2.mix&offset=4','PUT',b'5678',token)[1]['offset']==8
        assert pathlib.Path(staging,'ra2.mix').read_bytes()==b'12345678'
        assert call('/commit','POST',b'',token)[1]['state']=='queued'
        assert call('/chunk?name=ra2.mix&offset=8','PUT',b'x',token)[0]==400
        assert call('/status',token=token)[1]['received']==8
        threads=int(subprocess.check_output(['ps','-M','-p',str(p.pid)],text=True).count('\n')-1)
        assert threads<=40, threads
        p.stdin.write('\n');p.stdin.flush();p.wait(timeout=5);assert p.returncode==0
        print(f'PASS: access link, rejected missing/invalid credentials, origin, filename, chunks, resume, changed files, incomplete commit, publication handoff, shutdown; JVM threads={threads}')
    finally:
        watchdog.cancel()
        if p.poll() is None:timeout();p.wait()
