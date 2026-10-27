import { readFileSync } from 'node:fs';
const loads=JSON.parse(readFileSync('Library/BuildPlayerData/Player/RuntimeInitializeOnLoads.json','utf8')).root;
if(!loads.some(entry=>entry.className==='MobilePlayController'&&entry.methodName==='Register'))throw Error('Missing mobile startup callback.');
const unsupported=loads.filter(entry=>/mrutilitykit|Meta\.XR\.MultiplayerBlocks|Meta\.XR\.Audio/i.test(entry.assemblyName));
if(unsupported.length)throw Error('Unsupported native startup callbacks: '+JSON.stringify(unsupported));
console.log('PASS: mobile startup is present; MRUK/native audio/Meta multiplayer startup callbacks are absent.');
