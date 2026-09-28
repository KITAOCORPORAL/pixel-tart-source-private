# Face Lock Compare Spec

Face Lock 是本地、仅视图层的几何对齐。`FaceObservation` 保存 bounds、双眼、midpoint、eye distance、roll、confidence；`FaceLockPlanner` 按“有双眼→眼距→面积→Y/X→SubjectKey”确定 primary subject，避免多脸静默选错。变换只包含 translation、scale、roll，不写回源照片。

无脸、低置信度或不可得眼距返回明确 fallback，比较退回普通 2-Up。`TwoUpCompareState` 保留 viewport，Swap 不退出比较；Rapid Compare 通过 `RapidCompareState.PromoteChallenger` 将 B 晋升为 A。
