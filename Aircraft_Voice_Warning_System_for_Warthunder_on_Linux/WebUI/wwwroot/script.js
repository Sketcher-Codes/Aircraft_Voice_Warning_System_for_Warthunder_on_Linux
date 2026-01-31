//AI disclaimer - this whole file is generated with AI, I made some minor changes to increase the font size

(() => {
  const plot = document.getElementById('plot');
  const ctx = plot.getContext('2d');
  const controls = document.getElementById('controls');
  const cctx = controls.getContext('2d');
  const rudder = document.getElementById('rudder');
  const rctx = rudder.getContext('2d');
  const aoa = document.getElementById('aoa');
  const actx = aoa.getContext('2d');
  const gforce = document.getElementById('gforce');
  const gctx = gforce.getContext('2d');

  const flapsLight = document.getElementById('flapsLight');
  const gearLight = document.getElementById('gearLight');

  const samples = []; // {t, tas, alt}
  const historySecs = 60 * 1000;

  async function fetchTelemetry(){
    try{
      const r = await fetch('/telemetry');
      const j = await r.json();
      const t = Date.now();
      samples.push({t, tas: j.TAS_kmh, alt: j.Altitude_m, aileron: j.Aileron, elevator: j.Elevator, rudder: j.Rudder, aoa: j.AoA, g: j.G, flaps: j.Flaps, gear: j.Gear});
      const cutoff = t - historySecs;
      while(samples.length && samples[0].t < cutoff) samples.shift();
    }catch(e){/* ignore */}
  }

  setInterval(fetchTelemetry, 200);

  function draw(){
    requestAnimationFrame(draw);
    drawPlot(); drawControls(); drawRudder(); drawAoA(); drawG(); drawLights();
  }

  function drawPlot(){
    const w = plot.width, h = plot.height;
    ctx.clearRect(0,0,w,h);
    ctx.fillStyle='#07101a'; ctx.fillRect(0,0,w,h);
    // background grid
    ctx.strokeStyle='#19303a'; ctx.lineWidth=1;
    for(let x=0;x<w;x+=100){ ctx.beginPath(); ctx.moveTo(x,0); ctx.lineTo(x,h); ctx.stroke(); }
    // time range
    const now = Date.now();
    const t0 = now - historySecs;
    // compute ranges
    let minAlt=Infinity,maxAlt=-Infinity,minTas=Infinity,maxTas=-Infinity;
    for(const s of samples){ if(s.alt<minAlt) minAlt=s.alt; if(s.alt>maxAlt) maxAlt=s.alt; if(s.tas<minTas) minTas=s.tas; if(s.tas>maxTas) maxTas=s.tas; }
    if(!isFinite(minAlt)){ minAlt=0; maxAlt=1000; minTas=0; maxTas=200; }
    // pad
    const altPad = Math.max(10,(maxAlt-minAlt)*0.1);
    minAlt -= altPad; maxAlt += altPad;
    const tasPad = Math.max(5,(maxTas-minTas)*0.1);
    minTas -= tasPad; maxTas += tasPad;

    // axes labels left altitude, right tas (use same colour as graph lines)
    ctx.font='16px Arial';
    // protect against zero ranges
    if (maxAlt === minAlt) { maxAlt = minAlt + 1; }
    if (maxTas === minTas) { maxTas = minTas + 1; }

    // left axis label (vertical) and min/max ticks in same colour as altitude line
    ctx.save();
    ctx.fillStyle='#00ff66';
    ctx.translate(18,h/2); ctx.rotate(-Math.PI/2);
    ctx.fillText('Altitude (m)',0,0);
    ctx.restore();
    ctx.fillStyle='#00ff66'; ctx.fillText(maxAlt.toFixed(0),6,18);
    ctx.fillText(minAlt.toFixed(0),6,h-6);

    // right axis label (vertical) and min/max ticks in same colour as TAS line
    ctx.save();
    ctx.fillStyle='#ffcc00';
    ctx.translate(w-18,h/2); ctx.rotate(-Math.PI/2);
    ctx.fillText('Airspeed (km/h)',0,0);
    ctx.restore();
    ctx.fillStyle='#ffcc00'; ctx.fillText(maxTas.toFixed(0),w-72,18);
    ctx.fillText(minTas.toFixed(0),w-72,h-6);

    // draw altitude polyline
    ctx.beginPath();
    for(let i=0;i<samples.length;i++){
      const s = samples[i];
      const x = (s.t - t0)/historySecs * w;
      const y = h - (s.alt - minAlt)/(maxAlt - minAlt) * h;
      if(i===0) ctx.moveTo(x,y); else ctx.lineTo(x,y);
    }
    ctx.strokeStyle='#00ff66'; ctx.lineWidth=2; ctx.stroke();

    // draw tas polyline (right axis)
    ctx.beginPath();
    for(let i=0;i<samples.length;i++){
      const s = samples[i];
      const x = (s.t - t0)/historySecs * w;
      const y = h - (s.tas - minTas)/(maxTas - minTas) * h;
      if(i===0) ctx.moveTo(x,y); else ctx.lineTo(x,y);
    }
    ctx.strokeStyle='#ffcc00'; ctx.lineWidth=2; ctx.stroke();
  }

  function drawControls(){
    const w=controls.width,h=controls.height; cctx.clearRect(0,0,w,h);
    cctx.fillStyle='#07101a'; cctx.fillRect(0,0,w,h);
    const last = samples[samples.length-1] || {aileron:0,elevator:0};
    // center
    cctx.strokeStyle='#444'; cctx.strokeRect(10,10,w-20,h-20);
    cctx.beginPath();
    const cx = w/2 + (last.aileron/100)*(w/2-20);
    // invert elevator so pushing forward displays downwards (typical stick mapping)
    const cy = h/2 + (last.elevator/100)*(h/2-20);
    cctx.fillStyle='#0ff'; cctx.arc(cx,cy,8,0,Math.PI*2); cctx.fill();
  }

  function drawRudder(){
    const w=rudder.width,h=rudder.height; rctx.clearRect(0,0,w,h);
    rctx.fillStyle='#07101a'; rctx.fillRect(0,0,w,h);
    rctx.strokeStyle='#444'; rctx.strokeRect(10,10,w-20,h-20);
    const last = samples[samples.length-1] || {rudder:0};
    const x = Math.max(10 + 6, Math.min(w-10-6, w/2 + (last.rudder/100)*(w/2-20)));
    rctx.fillStyle='#0ff'; rctx.fillRect(x-6,h/2-8,12,16);
  }

  function drawAoA(){
    const w=aoa.width,h=aoa.height; actx.clearRect(0,0,w,h); actx.fillStyle='#07101a'; actx.fillRect(0,0,w,h);
    const last = samples[samples.length-1] || {aoa:0};
    // dial
    actx.strokeStyle='#666'; actx.beginPath(); actx.arc(w/2,h/2,70,0,Math.PI*2); actx.stroke();
    // needle
    // Map AoA so 0 starts on the left (PI) and positive values rotate clockwise.
    const maxAoA = 40;
    const angle = Math.PI + ((last.aoa / maxAoA) * (Math.PI / 2)); // left +/- 90deg
    actx.strokeStyle='#ff6'; actx.lineWidth=4; actx.beginPath(); actx.moveTo(w/2,h/2); actx.lineTo(w/2 + Math.cos(angle)*60, h/2 + Math.sin(angle)*60); actx.stroke();
    actx.fillStyle='#8cf'; actx.font='16px Arial'; actx.fillText('AoA: '+(last.aoa||0).toFixed(1)+'°',10,16);
  }

  function drawG(){
    const w=gforce.width,h=gforce.height; gctx.clearRect(0,0,w,h); gctx.fillStyle='#07101a'; gctx.fillRect(0,0,w,h);
    const last = samples[samples.length-1] || {g:0};
    gctx.strokeStyle='#666'; gctx.beginPath(); gctx.arc(w/2,h/2,70,0,Math.PI*2); gctx.stroke();
    const maxG = 13;
    // 0 starts on the left (PI); positive G rotates clockwise
    const angle = Math.PI + ((last.g / maxG) * (Math.PI / 2));
    gctx.strokeStyle='#ff6'; gctx.lineWidth=4; gctx.beginPath(); gctx.moveTo(w/2,h/2); gctx.lineTo(w/2 + Math.cos(angle)*60, h/2 + Math.sin(angle)*60); gctx.stroke();
    gctx.fillStyle='#8cf'; gctx.font='16px Arial'; gctx.fillText('G: '+(last.g||0).toFixed(2),10,16);
  }

  function drawLights(){
    const last = samples[samples.length-1] || {flaps:0,gear:0};
    updateLight(flapsLight,last.flaps);
    updateLight(gearLight,last.gear);
  }

  function updateLight(elem, value){
    elem.classList.remove('red'); elem.classList.remove('flash');
    if(value === 0){ /* unlit */ }
    else if(value > 0 && value < 100){ elem.classList.add('flash'); }
    else if(Math.abs(value - 100) < 0.001){ elem.classList.add('red'); }
  }

  draw();
})();
