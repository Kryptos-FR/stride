# Stride.Core.MicroThreading

Cooperative multitasking and coroutine infrastructure for the Stride game engine. Powers the async scripting model used by `AsyncScript`.

- `Scheduler` — cooperative task scheduler running on a single thread
- `MicroThread` — lightweight coroutine with full async/await support
- `Channel<T>` — async producer-consumer communication between micro-threads
- `AsyncSignal` — synchronization primitive for micro-thread coordination
- `MicroThreadLocal<T>` — async-aware equivalent of `ThreadLocal<T>`
