import assert from "node:assert/strict";
import {startRealtime,uniqueById} from "../frontend/lib/realtime-connection.ts";
import {startNotificationPolling} from "../frontend/lib/notification-polling.ts";
let checks=0;
const check=(value,name)=>{assert.ok(value,name);checks++;console.log("PASS "+name);};
const settle=async()=>{for(let i=0;i<25;i++)await Promise.resolve();};
function fixture({ticket,fail=false,ready=true}={}){
 let failJoin=false,deferredJoin;
 const timers=new Map(),listeners=new Map(),states=[],hubs=[];let id=0,now=0,visible=true,changes=0,readyCalls=0;
 const schedule=(callback,delay)=>{const key=++id;timers.set(key,{callback,delay,time:now+delay});return key;};
 const env={schedule,cancel:key=>timers.delete(key),visible:()=>visible,ready:async()=>{readyCalls++;return ready;},
  changed:()=>changes++,state:value=>states.push(value),listen:(event,callback)=>{listeners.set(event,callback);return()=>listeners.delete(event);},
  create:async()=>{const callbacks={};const hub={starts:0,stops:0,invocations:[],on:(method,fn)=>callbacks[method]=fn,
   onclose:fn=>callbacks.close=fn,onreconnecting:fn=>callbacks.reconnecting=fn,onreconnected:fn=>callbacks.reconnected=fn,
   start:async()=>{hub.starts++;if(fail){fail=false;throw Error('isolated transport failure');}},stop:async()=>{hub.stops++;callbacks.close?.();},
   invoke:async(method,...args)=>{hub.invocations.push([method,...args]);if(deferredJoin){const pending=deferredJoin;deferredJoin=undefined;await pending;}if(failJoin){failJoin=false;throw Error('isolated subscription failure');}},callbacks};hubs.push(hub);return hub;}};
 const stop=startRealtime(ticket?'ticket':'notifications',env,ticket);
 return{env,timers,hubs,states,stop,changes:()=>changes,readyCalls:()=>readyCalls,setVisible:value=>{visible=value;listeners.get('visibilitychange')?.();},
  advance:async ms=>{now+=ms;for(const[key,timer]of [...timers])if(timer.time<=now){timers.delete(key);timer.callback();await settle();}},
  event:(event)=>listeners.get(event)?.(),ready:value=>{ready=value;},failJoin:()=>{failJoin=true;},deferJoin:promise=>{deferredJoin=promise;}};
}
const a=fixture();await settle();
check(a.hubs.length===1&&a.states.at(-1)==='connected'&&a.changes()===1,'realtime initial connect reconciles authoritative HTTP state');
const emit=(target,kind,eventId)=>target.callbacks.Changed({kind,eventId});
emit(a.hubs[0],'notifications','a'.repeat(64));emit(a.hubs[0],'notifications','a'.repeat(64));emit(a.hubs[0],'notifications','b'.repeat(64));
await a.advance(75);
check(a.changes()===2,'realtime repeated IDs and bursts coalesce into one refresh without duplicate rendering');
emit(a.hubs[0],'ticket','c'.repeat(64));emit(a.hubs[0],'notifications','bad');await a.advance(100);
check(a.changes()===2,'realtime malformed hints and another area cannot trigger this subscription');
a.hubs[0].callbacks.reconnecting();await settle();
check(a.states.at(-1)==='reconnecting'&&a.readyCalls()===2,'realtime reconnect uses the existing session renewal abstraction');
a.hubs[0].callbacks.reconnected();await settle();
check(a.states.at(-1)==='connected'&&a.changes()===3,'realtime reconnect reconciles missed persistent changes');
a.setVisible(false);await settle();await a.advance(300000);
check(a.hubs[0].stops===1&&a.hubs.length===1&&a.timers.size===0,'realtime hidden tabs stop transport and scheduled retry work');
a.setVisible(true);await settle();
check(a.hubs.length===2&&a.states.at(-1)==='connected','realtime visible tabs reconnect automatically');
a.stop();await settle();
check(a.hubs[1].stops===1&&a.timers.size===0,'realtime unmount closes connections and timers');

const ticket='ed8f66aa-24c9-4214-abce-6d9c145bf33a';
const t=fixture({ticket});await settle();
check(JSON.stringify(t.hubs[0].invocations)===JSON.stringify([['JoinTicket',ticket]]),'realtime support invokes only authorized subscription, never message submission');
t.hubs[0].callbacks.reconnected();await settle();
check(t.hubs[0].invocations.length===2&&t.changes()===2,'realtime support rejoins its ticket and reloads after reconnect');
t.failJoin();t.hubs[0].callbacks.reconnected();await settle();
check(t.states.at(-1)==='offline'&&t.timers.size===1,'realtime failed resubscription closes the unusable connection and keeps fallback active');
await t.advance(2000);
check(t.states.at(-1)==='connected'&&t.hubs.length===2,'realtime failed resubscription recovers with a bounded new connection attempt');
t.stop();
const race=fixture({ticket});await settle();let rejectJoin;
race.deferJoin(new Promise((resolve,reject)=>{rejectJoin=reject;}));race.hubs[0].callbacks.reconnected();await settle();
race.setVisible(false);await settle();race.setVisible(true);await settle();
rejectJoin(Error('old connection completed late'));await settle();
check(race.hubs.length===2&&race.hubs[1].stops===0&&race.states.at(-1)==='connected','realtime late failure from an old connection cannot close its replacement');race.stop();
const failed=fixture({fail:true});await settle();
check(failed.states.at(-1)==='offline'&&[...failed.timers.values()][0].delay===2000,'realtime failed startup leaves fallback available and schedules a bounded retry');
await failed.advance(2000);
check(failed.hubs.length===2&&failed.states.at(-1)==='connected','realtime startup failure recovers without reloading the page');failed.stop();
const denied=fixture({ready:false});await settle();
check(denied.hubs.length===0&&denied.states.at(-1)==='offline','realtime does not connect without a valid existing session');
for(let i=0;i<5;i++){await denied.advance(30000);}
check([...denied.timers.values()].every(timer=>timer.delay>=2000&&timer.delay<=30000),'realtime unavailable sessions cannot create a tight retry loop');
denied.ready(true);denied.event('online');await settle();
check(denied.states.at(-1)==='connected','realtime online/focus recovery retries existing authentication');denied.stop();

let resolve;let stopped=0,lateChanges=0;
const lateStop=startRealtime('notifications',{create:()=>new Promise(r=>resolve=r),ready:async()=>true,visible:()=>true,
 schedule:()=>0,cancel:()=>{},listen:()=>()=>{},state:()=>{},changed:()=>lateChanges++});
await settle();lateStop();resolve({stop:async()=>stopped++});await settle();
check(stopped===1&&lateChanges===0,'realtime late async imports cannot resurrect an unmounted identity scope');
check(JSON.stringify(uniqueById([{id:'b',text:2},{id:'a',text:1},{id:'b',text:2}]).map(row=>row.id))===JSON.stringify(['b','a']),
 'realtime live/refresh reconciliation deduplicates IDs while preserving authoritative message order');

const poll=fixture({ready:false});poll.stop();poll.timers.clear();let live=false,reads=0;const mutation=new Map();
const end=startNotificationPolling(async()=>{reads++;return true;},{visible:()=>true,interval:()=>live?120000:20000,
 schedule:poll.env.schedule,cancel:poll.env.cancel,listen:(event,callback)=>{mutation.set(event,callback);return()=>mutation.delete(event);}});
await settle();check([...poll.timers.values()][0].delay===20000,'realtime disconnected notification/support polling retains the normal 20-second fallback');
live=true;mutation.get('mutation')();await settle();
check([...poll.timers.values()][0].delay===120000,'realtime connected transport reduces polling to a lightweight reconciliation interval');
live=false;mutation.get('mutation')();await settle();
check([...poll.timers.values()][0].delay===20000&&reads===3,'realtime lost connection resumes fallback promptly without overlapping fetches');end();
console.log(`${checks} realtime client checks passed; no network, database, SMS or push used.`);
