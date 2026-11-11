// Executed only by the user's context-menu action or explicit Scan Current Page click.
// No network requests: previews use only already decoded same-origin page pixels.
export function collectPageImages(clickedUrl=null,limit=500){
 const output=[];let previews=0;
 const images=Array.from(document.images).filter(img=>!clickedUrl||img.currentSrc===clickedUrl||img.src===clickedUrl).slice(0,limit);
 for(const img of images){
  const attr=name=>img.getAttribute(name)||"";
  // Attribute subset adapted from Image Harvest c87e2305, MIT; see third-party/ImageHarvest-MIT.txt.
  const lazy=attr("data-original")||attr("data-full-src")||attr("data-hi-res-src")||attr("data-src")||attr("data-lazy-src")||attr("data-lazy")||attr("data-image")||"";
  const lazySet=attr("data-srcset")||attr("data-lazy-srcset")||"";
  let thumbnailDataUrl=null;
  if(img.complete&&img.naturalWidth>0&&previews<200){try{
   const canvas=document.createElement("canvas"),ratio=Math.min(1,160/img.naturalWidth,120/img.naturalHeight);canvas.width=Math.max(1,Math.round(img.naturalWidth*ratio));canvas.height=Math.max(1,Math.round(img.naturalHeight*ratio));canvas.getContext("2d").drawImage(img,0,0,canvas.width,canvas.height);const data=canvas.toDataURL("image/jpeg",.65);if(data.length<=18000){thumbnailDataUrl=data;previews++;}
  }catch{}}
  output.push({src:img.src,currentSrc:img.currentSrc,srcset:img.srcset,lazySrc:lazy,lazySrcset:lazySet,
   naturalWidth:img.complete?img.naturalWidth:0,naturalHeight:img.complete?img.naturalHeight:0,
   declaredWidth:Number(attr("data-width"))||null,declaredHeight:Number(attr("data-height"))||null,
   alt:img.alt||"",thumbnailDataUrl,sources:Array.from(img.parentElement?.tagName==="PICTURE"?img.parentElement.querySelectorAll("source"):[]).map(s=>({srcset:s.getAttribute("data-srcset")||s.srcset,active:!s.media||matchMedia(s.media).matches}))});
 }
 return{pageUrl:location.href,pageTitle:document.title,images:output,totalDomImages:document.images.length,truncated:images.length<document.images.length&&!clickedUrl};
}
