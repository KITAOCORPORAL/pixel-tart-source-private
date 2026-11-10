export const MAX_IMAGE_BYTES=16*1024*1024;
export function httpUrl(value,base){const u=new URL(value,base);if(!["http:","https:"].includes(u.protocol)||u.username||u.password)throw new Error("首版只采集HTTP/HTTPS图片，来源地址无效。");return u.href;}
export function parseSrcset(value,base){
 const rows=[];let p=0;
 while(p<value.length){
  while(p<value.length&&/[\s,]/.test(value[p]))p++;
  if(p>=value.length)break;
  let url="";while(p<value.length&&!/\s/.test(value[p]))url+=value[p++];
  let descriptors="";
  if(url.endsWith(","))url=url.replace(/,+$/,"");
  else{while(p<value.length&&value[p]!==",")descriptors+=value[p++];if(value[p]===",")p++;}
  const tokens=descriptors.trim().split(/\s+/).filter(Boolean);
  if(tokens.length>1||tokens.length===1&&!/^(?:[1-9]\d*w|(?:\d+(?:\.\d+)?|\.\d+)x)$/.test(tokens[0]))throw new Error("无法可靠解析此图片的srcset，未改采缩略图。");
  const descriptor=tokens[0]||"1x";const kind=descriptor.at(-1);const rank=Number(descriptor.slice(0,-1));
  if(!Number.isFinite(rank)||rank<=0)throw new Error("srcset尺寸无效。");
  rows.push({url:httpUrl(url,base),kind,rank});
 }
 if(rows.length===0||new Set(rows.map(r=>r.kind)).size!==1)throw new Error("srcset尺寸类型不一致，未改采缩略图。");
 return rows;
}
export function chooseOriginal(info){
 const clicked=httpUrl(info.clickedUrl,info.pageUrl);
 const matching=info.images.filter(image=>[image.currentSrc,image.src].some(u=>{try{return httpUrl(u,info.pageUrl)===clicked;}catch{return false;}}));
 if(!matching.length)return{imageUrl:clicked,renderedImageUrl:clicked,sourceSelection:"direct"};
 const choices=matching.map(image=>{
  let set=image.srcset;
  for(const source of image.sources||[]){if(!source.active||!source.srcset)continue;const rows=parseSrcset(source.srcset,info.pageUrl);if(rows.some(x=>x.url===clicked)){set=source.srcset;break;}}
  if(!set?.trim())return{imageUrl:clicked,renderedImageUrl:clicked,sourceSelection:"direct"};
  const best=parseSrcset(set,info.pageUrl).sort((a,b)=>b.rank-a.rank)[0];return{imageUrl:best.url,renderedImageUrl:clicked,sourceSelection:"srcset-"+best.rank+best.kind};
 });
 if(new Set(choices.map(x=>x.imageUrl)).size!==1)throw new Error("同一缩略图对应多个原图，无法确定本次图片，未自动替代。");
 return choices[0];
}
export function validateReceipt(row,reply){
 const receipt=reply?.receipt,d=row.destination;
 if(!reply?.ok||reply.code!=="Completed"||!receipt||receipt.captureId!==row.id||receipt.libraryId!==d.libraryId
   ||(receipt.projectId||null)!==(d.projectId||null)||(receipt.boardId||null)!==(d.boardId||null)
   ||!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(receipt.assetId||"")||receipt.assetId==="00000000-0000-0000-0000-000000000000"||!['Imported','Reused'].includes(receipt.outcome)
   ||receipt.contentSha256?.toLowerCase()!==row.contentSha256?.toLowerCase())throw new Error("没有收到匹配本次图片和目的地的入库回执，保留待处理。");
 return receipt;
}
