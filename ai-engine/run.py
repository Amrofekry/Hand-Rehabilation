"""One-command reproducibility using the isolated interpreter; no policy changes."""
import os,subprocess,sys
from pathlib import Path
root=Path(__file__).resolve().parent;project=root.parent
python=root/'.venv-ai/Scripts/python.exe';task=sys.argv[1] if len(sys.argv)>1 else 'test'
if task not in {'setup','acquire','train','test','all'}:raise SystemExit('Tasks: setup, acquire, train, test, all')
env=dict(os.environ);env['PYTHONPATH']=str(root/'src');env['PYTHONIOENCODING']='utf-8'
temp=project/'Logs/ai-temp';temp.mkdir(exist_ok=True);env['TEMP']=env['TMP']=str(temp)
env.pop('SSLKEYLOGFILE',None)
def run(args):subprocess.run([str(x) for x in args],cwd=project,env=env,check=True)
if task=='setup':
 run([project/'.venv/Scripts/python.exe','-m','venv',root/'.venv-ai'])
 run([python,'-m','pip','install','-r',root/'requirements-lock.txt']);raise SystemExit(0)
if not python.exists():raise SystemExit('Run setup first')
if task in {'acquire','all'}:run([python,'-c',f'from fatigue_ai.data import acquire;acquire({str(root)!r})'])
if task in {'train','all'}:
 run([python,'-m','fatigue_ai.train','--root',root]);run([python,root/'export_unity.py'])
if task in {'test','all'}:run([python,'-m','pytest',root/'tests','-q','--basetemp',project/'Logs/ai-test-python-entry'])
