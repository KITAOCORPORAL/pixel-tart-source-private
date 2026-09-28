# Booking Domain Model

复用 `ShootBooking`。本轮补充 PreBufferMinutes、PostBufferMinutes、HoldExpiresAtUtc、PaymentState、Revision、DeviceId、DeletedAtUtc。现有状态映射：Tentative=HOLD、Confirmed/Preparing=CONFIRMED、Shooting=LOCKED、Cancelled=CANCELLED、Completed=COMPLETED。

`BookingConflictRules` 在平台无关 Core 中计算 buffered interval，返回 HardOverlap、BufferOverlap、HoldOverlap、LockedOverlap 的结构化结果。过期 HOLD 保留历史记录。

## Persistence status

Schema migration v6 and `SqliteShootBookingRepository` persist buffers, HOLD expiry, payment state, revision, device id, and DeletedAt tombstone. Existing migration backup behavior is reused. The current editor exposes status, buffers, HOLD expiry text, and payment state; structured conflict classification remains in Core and existing conflict panel.
