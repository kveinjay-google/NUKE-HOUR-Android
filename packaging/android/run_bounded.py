"""Run a build/test with an external deadline and a log; stop only its process group.

Use --threads only for small native smoke tests, not JVM/.NET/browser processes.
Build entry points must also pass their own explicit worker-count switches.
"""
import argparse
import os
from pathlib import Path
import signal
import subprocess
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--seconds', type=int, required=True)
    parser.add_argument('--log', type=Path, required=True)
    parser.add_argument('--threads', type=int)
    parser.add_argument('command', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ['--'] else args.command
    if not command or args.seconds <= 0:
        parser.error('A command and a positive timeout are required.')
    if args.threads is not None and not 1 <= args.threads <= 8:
        parser.error('Native smoke-test thread ceilings must be between 1 and 8.')

    environment = os.environ.copy()
    environment.update(
        DOTNET_ROLL_FORWARD='Major', DOTNET_PROCESSOR_COUNT='4',
        MSBUILDDISABLENODEREUSE='1',
        ANDROID_HOME=os.environ.get('ANDROID_HOME', '/opt/homebrew/share/android-commandlinetools'),
        JAVA_HOME=os.environ.get('JAVA_HOME', '/opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home'))
    if 'DOTNET_ROOT' not in environment and Path.home().joinpath('.dotnet').is_dir():
        environment['DOTNET_ROOT'] = str(Path.home() / '.dotnet')
    args.log.parent.mkdir(parents=True, exist_ok=True)
    started = time.monotonic()
    peak_threads = 0
    with args.log.open('w') as log:
        process = subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT,
                                   env=environment, start_new_session=True)
        failure = None
        try:
            while process.poll() is None:
                if time.monotonic() - started >= args.seconds:
                    raise TimeoutError('External deadline exceeded.')
                if args.threads is not None:
                    snapshot = subprocess.run(['ps', '-M', '-p', str(process.pid)],
                                              text=True, capture_output=True, timeout=5)
                    peak_threads = max(peak_threads, len(snapshot.stdout.splitlines()) - 1)
                    if peak_threads > args.threads:
                        raise RuntimeError('Native smoke-test thread ceiling exceeded.')
                time.sleep(0.25)
        except BaseException as error:
            failure = error
            # The group belongs to this launch; never terminate unrelated applications.
            try:
                os.killpg(process.pid, signal.SIGTERM)
            except ProcessLookupError:
                pass
            deadline = time.monotonic() + 5
            while time.monotonic() < deadline:
                process.poll()  # Reap the leader, but also wait for any remaining workers.
                try:
                    os.killpg(process.pid, 0)
                except ProcessLookupError:
                    break
                time.sleep(0.1)
            else:
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
            process.wait()
            log.write(f'\nWATCHDOG: {error}\n')
        result = 124 if failure is not None else process.returncode
    print(f'exit={result} seconds={time.monotonic() - started:.1f} '
          f'peak_threads={peak_threads} log={args.log}')
    return result


if __name__ == '__main__':
    raise SystemExit(main())
