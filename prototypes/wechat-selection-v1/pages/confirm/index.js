Page({submit(){wx.showModal({title:'再次确认',content:'确认提交本次选片结果？',success(result){if(result.confirm)wx.showToast({title:'原型提交完成'})}})},back(){wx.navigateBack()}});
