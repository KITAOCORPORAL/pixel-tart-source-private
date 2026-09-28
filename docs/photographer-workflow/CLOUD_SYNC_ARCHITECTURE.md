# Cloud Sync Architecture

Sync 使用稳定 StableId、Revision、CreatedAt/UpdatedAt、DeletedAt tombstone、DeviceId。`DeterministicSyncTransport` 是测试 transport；RAW/JPEG 默认 LOCAL ONLY。重复事件按 revision 幂等，乱序和重试不重复创建实体。

本轮没有真实 Firebase/Supabase/Azure/AWS 服务，也不上传摄影师 RAW 库。
