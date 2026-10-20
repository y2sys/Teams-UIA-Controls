import test from 'node:test';
import assert from 'node:assert/strict';
import { selectSession, presentation } from '../src/core.mjs';
const now = 10000;
const session = { id:'teams:1:2', platform:'teams', inMeeting:true, muted:true, cameraOn:false, sharing:false, capabilities:{microphone:true,camera:true,share:true,leave:true} };
const state = sessions => ({receivedAt:now,sessions});
test('auto never chooses between simultaneous calls',()=>{ assert.equal(selectSession(state([session,{...session,id:'zoom:2'}]),'auto',now).reason,'ambiguous'); });
test('explicit target cannot fall through to another call',()=>{ assert.equal(selectSession(state([session]),'teams:gone',now).reason,'missing'); });
test('stale or failed state cannot authorize actions',()=>{ assert.equal(selectSession(state([session]),'auto',now+4001).reason,'unknown'); assert.equal(selectSession({...state([session]),error:'failed'},'auto',now).reason,'unknown'); });
test('picker does not display sharing as active',()=>{ assert.equal(presentation('share',{session:{...session,picker:true}}).text,'共有先選択中'); });
test('picker preserves microphone, camera and leave appearance',()=>{ for (const kind of ['microphone','camera','leave']) assert.deepEqual(presentation(kind,{session:{...session,picker:true}}),presentation(kind,{session})); });
test('missing state during picker opening cannot overwrite last confirmed appearance',()=>{
  const transition = {...session,transition:true,muted:null,cameraOn:null,sharing:null,capabilities:{},lastKnown:session};
  for (const kind of ['microphone','camera','leave']) {
    assert.deepEqual(presentation(kind,{session:transition}),presentation(kind,{session}));
    assert.deepEqual(presentation(kind,{session:{...transition,picker:true}}),presentation(kind,{session}));
  }
  assert.equal(presentation('share',{session:{...transition,picker:true}}).color,'#eab308');
});
test('unknown microphone state is not shown as live or merely disabled',()=>{ assert.equal(presentation('microphone',{session:{...session,muted:null,capabilities:{microphone:false}}}).text,'状態不明'); });
test('only observed microphone state drives icon',()=>{ assert.equal(presentation('microphone',{session}).text,'ミュート'); assert.equal(presentation('microphone',{session:{...session,muted:false}}).text,'マイク ON'); });
test('idle buttons reflect no meeting',()=>{ assert.equal(selectSession(state([]),'auto',now).reason,'idle'); });
