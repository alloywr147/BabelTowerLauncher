import * as THREE from 'three';

const fragmentShader = `
precision highp float;
uniform vec2 resolution;
uniform vec2 pointer;
uniform float time;
uniform float hover;
uniform float theme;
varying vec2 vUv;

float terrain(vec2 p) {
  vec2 q=p;
  q.x+=.16*sin(p.y*1.75+time*.075)+.075*cos(p.y*3.3-time*.045);
  q.y+=.13*sin(p.x*1.6-time*.06);
  float a=1.18*exp(-dot((q-vec2(-.78,.08))*vec2(.93,1.16),(q-vec2(-.78,.08))*vec2(.93,1.16)));
  float b=.76*exp(-dot((q-vec2(.05,-.83))*vec2(1.65,1.28),(q-vec2(.05,-.83))*vec2(1.65,1.28)));
  float c=.91*exp(-dot((q-vec2(-1.62,.96))*vec2(1.2,1.08),(q-vec2(-1.62,.96))*vec2(1.2,1.08)));
  float d=.1*sin(q.x*2.6+q.y*1.8)+.065*cos(q.y*3.5-q.x*.65);
  float r=length(p-pointer);
  float ripple=.05*hover*sin(r*10.-time*1.55)*exp(-r*r*1.3);
  return a+b+c+d+ripple;
}
void main() {
  vec2 p=(vUv-.5)*vec2(resolution.x/resolution.y,1.)*3.3;
  float height=terrain(p);
  float levels=height*24.;
  float aa=max(fwidth(levels),.006);
  float distanceToLine=abs(fract(levels+.5)-.5);
  float line=1.-smoothstep(aa*.22,aa*1.05,distanceToLine);
  float majorDistance=abs(fract(levels/5.+.5)-.5)*5.;
  float major=1.-smoothstep(aa*.35,aa*1.5,majorDistance);
  vec3 paper=mix(vec3(.918,.898,.882),vec3(.061,.086,.099),theme);
  vec3 ink=mix(vec3(.40,.47,.45),vec3(.36,.49,.50),theme);
  vec3 gold=mix(vec3(.60,.44,.28),vec3(.71,.57,.37),theme);
  float glow=exp(-dot(p-vec2(-.72,.15),p-vec2(-.72,.15))*.65);
  vec3 color=paper+mix(vec3(-.015,-.013,-.012),vec3(.013,.025,.027),theme)*glow;
  // The existing UI gradient stays unchanged; density also tapers behind its text.
  float quiet=1.-smoothstep(-.25,1.45,p.x);
  float edge=smoothstep(-.18,.1,height);
  color=mix(color,ink,line*(.26+.1*glow)*quiet*edge);
  color=mix(color,gold,major*.42*quiet*edge);
  vec2 grid=abs(fract(vUv*resolution/72.+.5)-.5);
  float gridLine=1.-smoothstep(0.,.011,min(grid.x,grid.y));
  color=mix(color,ink,gridLine*.035*quiet);
  float cursorGlow=exp(-dot(p-pointer,p-pointer)*2.8)*hover;
  color+=mix(vec3(-.009),vec3(.012,.015,.011),theme)*cursorGlow*quiet;
  gl_FragColor=vec4(color,1.);
}`;

export class ContourScene {
  constructor(container) {
    this.container=container;
    this.themeAmount=1;
    this.themeTarget=1;
    this.hoverAmount=0;
    this.hoverTarget=0;
    this.previousTime=0;
    this.frames=0;
    this.lastCell=null;
    this.renderer=new THREE.WebGLRenderer({ antialias:true, alpha:false });
    this.renderer.setPixelRatio(Math.min(devicePixelRatio,1.5));
    this.renderer.outputColorSpace=THREE.LinearSRGBColorSpace;
    this.renderer.domElement.setAttribute('aria-hidden','true');
    container.appendChild(this.renderer.domElement);
    this.uniforms={
      resolution:{value:new THREE.Vector2(1,1)}, pointer:{value:new THREE.Vector2(-.75,.1)},
      time:{value:0}, hover:{value:0}, theme:{value:1}
    };
    this.material=new THREE.ShaderMaterial({
      uniforms:this.uniforms,
      vertexShader:'varying vec2 vUv; void main(){vUv=uv;gl_Position=vec4(position.xy,0.,1.);}',
      fragmentShader,
      depthTest:false, depthWrite:false,
    });
    this.geometry=new THREE.PlaneGeometry(2,2);
    this.world=new THREE.Scene();
    this.world.add(new THREE.Mesh(this.geometry,this.material));
    this.camera=new THREE.Camera();
    this.resize();
  }
  async load() { this.update(performance.now()/1000); }
  setTheme(dark,immediate=false) {
    this.themeTarget=Number(dark);
    if(immediate) this.themeAmount=this.themeTarget;
  }
  setReduced(reduced) { this.reduced=reduced; }
  setLauncherPointer(x,y) {
    const box=this.container.getBoundingClientRect();
    this.uniforms.pointer.value.set(((x-box.left)/box.width-.5)*(box.width/box.height)*3.3, (.5-(y-box.top)/box.height)*3.3);
    this.hoverTarget=1;
    const cell=Math.floor(x/100)+Math.floor(y/100)*100;
    if(cell!==this.lastCell) { this.lastCell=cell;this.onHover?.(cell); }
  }
  clearLauncherPointer() { this.hoverTarget=0;this.lastCell=null; }
  resize() {
    const width=this.container.clientWidth,height=this.container.clientHeight;
    if(!width||!height) return;
    this.renderer.setSize(width,height);
    this.uniforms.resolution.value.set(width,height);
    // Resizing must also repaint a paused preview instead of exposing a cleared canvas.
    this.renderer.render(this.world,this.camera);
  }
  update(time) {
    const elapsed=Math.min(.1,Math.max(0,this.previousTime?time-this.previousTime:1/60));
    this.previousTime=time;
    this.themeAmount+=(this.themeTarget-this.themeAmount)*(1-Math.exp(-elapsed*5));
    if(Math.abs(this.themeTarget-this.themeAmount)<.0003) this.themeAmount=this.themeTarget;
    this.hoverAmount+=(this.hoverTarget-this.hoverAmount)*(1-Math.exp(-elapsed*4));
    this.uniforms.time.value=time;
    this.uniforms.theme.value=this.themeAmount;
    this.uniforms.hover.value=this.hoverAmount;
    this.renderer.render(this.world,this.camera);
    this.frames++;
  }
  get launcherTelemetry() { return {frames:this.frames,lift:this.hoverAmount*.22}; }
  dispose() {
    this.geometry.dispose();this.material.dispose();this.renderer.dispose();
    this.renderer.domElement.remove();
  }
}
