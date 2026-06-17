import{r as x,g as O}from"./react-vendor-C8w-UNLI.js";function g(n,a){for(var u=0;u<a.length;u++){const t=a[u];if(typeof t!="string"&&!Array.isArray(t)){for(const o in t)if(o!=="default"&&!(o in n)){const s=Object.getOwnPropertyDescriptor(t,o);s&&Object.defineProperty(n,o,s.get?s:{enumerable:!0,get:()=>t[o]})}}}return Object.freeze(Object.defineProperty(n,Symbol.toStringTag,{value:"Module"}))}var _={exports:{}},i={};/**
 * @license React
 * react-jsx-runtime.production.min.js
 *
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */var d;function j(){if(d)return i;d=1;var n=x(),a=Symbol.for("react.element"),u=Symbol.for("react.fragment"),t=Object.prototype.hasOwnProperty,o=n.__SECRET_INTERNALS_DO_NOT_USE_OR_YOU_WILL_BE_FIRED.ReactCurrentOwner,s={key:!0,ref:!0,__self:!0,__source:!0};function l(f,e,m){var r,p={},c=null,R=null;m!==void 0&&(c=""+m),e.key!==void 0&&(c=""+e.key),e.ref!==void 0&&(R=e.ref);for(r in e)t.call(e,r)&&!s.hasOwnProperty(r)&&(p[r]=e[r]);if(f&&f.defaultProps)for(r in e=f.defaultProps,e)p[r]===void 0&&(p[r]=e[r]);return{$$typeof:a,type:f,key:c,ref:R,props:p,_owner:o.current}}return i.Fragment=u,i.jsx=l,i.jsxs=l,i}var y;function b(){return y||(y=1,_.exports=j()),_.exports}var S=b(),v=x();const E=O(v),h=g({__proto__:null,default:E},[v]);export{E as R,h as a,S as j,v as r};
