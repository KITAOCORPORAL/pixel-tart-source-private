import{chooseOriginal,parseSrcset,httpUrl}from"./source.mjs";
export function formatFromUrl(value){let path;try{path=new URL(value).pathname.toLowerCase();}catch{return"UNKNOWN";}if(/\.jpe?g$/.test(path))return"JPEG";if(/\.png$/.test(path))return"PNG";if(/\.webp$/.test(path))return"WEBP";if(/\.gif$/.test(path))return"GIF";if(/\.svg$/.test(path))return"SVG";return"UNKNOWN";}
export function normalizePageScan(snapshot){
 const pageUrl=httpUrl(snapshot.pageUrl),items=[],byUrl=new Map();let ignored=0;
 for(const image of snapshot.images){try{
  const lazy=image.lazySrc?httpUrl(image.lazySrc,pageUrl):null;
  const set=image.lazySrcset||image.srcset;
  let rendered,placeholder=false;
  try{rendered=httpUrl(image.currentSrc||image.src,pageUrl);}catch(error){placeholder=true;if(lazy)rendered=lazy;else if(set)rendered=parseSrcset(set,pageUrl)[0].url;else throw error;}
  const base={...image,srcset:set,...(placeholder?{src:rendered,currentSrc:rendered}:{})};
  let chosen;
  if(set?.trim())chosen=chooseOriginal({pageUrl,clickedUrl:rendered,images:[base]});
  else if(lazy)chosen={imageUrl:lazy,renderedImageUrl:rendered,sourceSelection:"lazy-attribute"};
  else chosen=chooseOriginal({pageUrl,clickedUrl:rendered,images:[base]});
  if(placeholder)chosen.renderedImageUrl=null;
  let width=null,height=null,dimensionSource="原图尺寸未知";
  if(!placeholder&&!lazy&&chosen.imageUrl===rendered&&!set&&image.naturalWidth>0){width=image.naturalWidth;height=image.naturalHeight;dimensionSource="页面已加载图片";}
  else if(image.declaredWidth>0){width=image.declaredWidth;height=image.declaredHeight>0?image.declaredHeight:null;dimensionSource="网页懒加载尺寸声明";}
  else if(set){const row=parseSrcset(set,pageUrl).find(x=>x.url===chosen.imageUrl);if(row?.kind==="w"){width=row.rank;dimensionSource="srcset声明宽度，高度未知";}}
  const same=byUrl.get(chosen.imageUrl);if(same){same.occurrences++;continue;}
  const item={id:crypto.randomUUID(),imageUrl:chosen.imageUrl,renderedImageUrl:chosen.renderedImageUrl,sourceSelection:chosen.sourceSelection,
   thumbnailDataUrl:placeholder?null:image.thumbnailDataUrl||null,alt:image.alt||"",knownWidth:width,knownHeight:height,dimensionSource,
   format:formatFromUrl(chosen.imageUrl),formatSource:"链接格式推断，发送时由桌面验证",occurrences:1,selected:false,state:"candidate",error:"",captureId:null,receipt:null};
  items.push(item);byUrl.set(item.imageUrl,item);
 }catch(error){
  // An unparseable candidate remains explicit; do not silently fall back to its rendered thumbnail.
  items.push({id:crypto.randomUUID(),imageUrl:image.lazySrc||image.currentSrc||image.src,renderedImageUrl:image.currentSrc||image.src,thumbnailDataUrl:image.thumbnailDataUrl||null,knownWidth:null,knownHeight:null,dimensionSource:"原图尺寸未知",format:"UNKNOWN",occurrences:1,selected:false,state:"sourceRejected",error:error.message,captureId:null,receipt:null});ignored++;
 }}
 return{pageUrl,pageTitle:snapshot.pageTitle||"当前页",items,truncated:!!snapshot.truncated,totalDomImages:snapshot.totalDomImages,sourceRejected:ignored};
}
export function matchesFilter(item,filter){
 if(filter.format&&filter.format!=="ALL"&&item.format!==filter.format)return false;
 const minW=Number(filter.minWidth)||0,minH=Number(filter.minHeight)||0;
 if((minW&&!item.knownWidth)||(minH&&!item.knownHeight))return !!filter.includeUnknown;
 return(!minW||item.knownWidth>=minW)&&(!minH||item.knownHeight>=minH);
}
