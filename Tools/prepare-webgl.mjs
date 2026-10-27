// The game does not use Meta AI/voice features. Meta XR 83 ships these
// assemblies with WebGL compiler errors, so omit them only on WebGL.
import { readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
const cache='Library/PackageCache';
function packageDirectory(name){
  const folder=readdirSync(cache).find(folder=>folder.startsWith(name+'@'));
  if(!folder)throw Error(`Resolve Unity packages first: ${name}`);
  return join(cache,folder);
}
const core=packageDirectory('com.meta.xr.sdk.core');
const asm=join(core,'Scripts/BuildingBlocks/AIBlocks/Meta.XR.BuildingBlocks.AIBlocks.asmdef');
const definition=JSON.parse(readFileSync(asm,'utf8'));
if(!definition.excludePlatforms.includes('WebGL')){
  definition.excludePlatforms.push('WebGL');
  writeFileSync(asm,JSON.stringify(definition,null,4)+'\n');
}
function excludeWebGL(directory){
  for(const entry of readdirSync(directory,{withFileTypes:true})){
    const file=join(directory,entry.name);
    if(entry.isDirectory())excludeWebGL(file);
    else if(entry.name.endsWith('.asmdef')){
      const asm=JSON.parse(readFileSync(file,'utf8'));
      if(asm.includePlatforms?.length){
        if(asm.includePlatforms.includes('WebGL')){
          asm.defineConstraints??=[];
          const constraint='!UNITY_WEBGL || UNITY_EDITOR';
          if(!asm.defineConstraints.includes(constraint)){
            asm.defineConstraints.push(constraint);
            writeFileSync(file,JSON.stringify(asm,null,4)+'\n');
          }
        }
        continue;
      }
      asm.excludePlatforms??=[];
      if(!asm.excludePlatforms.includes('WebGL')){
        asm.excludePlatforms.push('WebGL');
        writeFileSync(file,JSON.stringify(asm,null,4)+'\n');
      }
    }
  }
}
const voice=packageDirectory('com.meta.xr.sdk.voice');
excludeWebGL(voice);
// MRUK runs InitializeSharedLibrary before the splash screen. Its WebGL
// loader returns without filling native function pointers, but startup still
// invokes SetLogPrinter/CreateGlobalContext, trapping with "null function".
excludeWebGL(packageDirectory('com.meta.xr.mrutilitykit'));
// These optional Meta helpers reference MRUK. The game uses its own Fusion
// launcher/avatar scripts instead, so they must be omitted together on WebGL.
excludeWebGL(join(core,'Scripts/BuildingBlocks/MultiplayerBlocks'));
// The unused voice plugin has the same filename as Photon's required plugin.
const pluginMeta=join(voice,'Lib/Wit.ai/Lib/third-party/NativeWebSocket/WebSocket/WebSocket.jslib.meta');
const plugin=readFileSync(pluginMeta,'utf8').replace(/(WebGL: WebGL\s+second:\s+enabled:) 1/,'$1 0');
writeFileSync(pluginMeta,plugin);
console.log('Meta XR WebGL compatibility prepared; VR platforms remain enabled.');
