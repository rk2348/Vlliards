// Receive an in-memory workflow request from a one-use local named pipe,
// then feed it to the publishing process through stdin. No credentials are
// placed in arguments, files, or logs.
import net from 'node:net';
import { spawn } from 'node:child_process';
import { readFileSync } from 'node:fs';
const [pipe,mode]=process.argv.slice(2);
if(!pipe?.startsWith('\\\\.\\pipe\\vlliards-')&&!pipe?.startsWith('http://127.0.0.1:'))throw Error('Invalid local input transport.');
const cwd='C:/Users/ryoma/Documents/Dev/Unity/Vlliards/Sites/vlliards-mobile';
const project=JSON.parse(readFileSync(cwd+'/.openai/hosting.json','utf8')).project_id;
const args=mode==='bootstrap'
  ? ['C:/Users/ryoma/Documents/Dev/Unity/Vlliards/Tools/bootstrap-site-source.mjs']
  : ['C:/Users/ryoma/.codex/plugins/cache/openai-curated-remote/sites/1.1.2/scripts/site-workflow.mjs','--project-id',project];
const env={...process.env,GIT_CONFIG_COUNT:'1',GIT_CONFIG_KEY_0:'safe.directory',GIT_CONFIG_VALUE_0:cwd};
function run(input){
  const child=spawn('C:/Program Files/nodejs/node.exe',args,{cwd,env,windowsHide:true,stdio:['pipe','inherit','inherit']});
  child.stdin.end(input+'\n');input='';
  child.on('error',error=>{console.error(error.message);process.exitCode=1;});
  child.on('close',code=>process.exitCode=code??1);
}
if(pipe.startsWith('http://127.0.0.1:')){
  const response=await fetch(pipe,{signal:AbortSignal.timeout(10000)});
  if(!response.ok)throw Error('Local input transport failed.');
  run(await response.text());
}else{
  let input='';
  const socket=net.connect(pipe);
  socket.setEncoding('utf8');
  socket.on('data',data=>input+=data);
  socket.on('error',error=>{console.error(error.message);process.exitCode=1;});
  socket.on('end',()=>run(input));
}
