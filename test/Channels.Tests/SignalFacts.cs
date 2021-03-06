﻿using Channels.Networking.Sockets.Internal;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Channels.Tests
{
    public class SignalFacts
    {
        [Fact]
        public void SignalIsNotCompletedByDefault()
        {
            Assert.False(new Signal().IsCompleted);
        }

        [Fact]
        public void SignalBecomesCompletedWhenSet()
        {
            var signal = new Signal();
            signal.Set();
            Assert.True(signal.IsCompleted);
        }

        [Fact]
        public void SignalBecomesNotCompletedWhenResultFetched()
        {
            var signal = new Signal();
            signal.Set();
            signal.GetResult();
            Assert.False(signal.IsCompleted);
        }

        [Fact]
        public void AlreadySetSignalManuallyAwaitableWithoutExternalCaller()
        {
            // here we're simulating the thread-race scenario:
            // thread A: checks IsCompleted, sees false
            // thread B: sets the status
            // thread A: asks for the awaiter and adds a continuation

            var signal = new Signal();

            // A
            Assert.False(signal.IsCompleted);

            // B
            signal.Set();

            int wasInvoked = 0;

            // A
            signal.OnCompleted(() =>
            {
                signal.GetResult(); // compiler awaiter always does this
                Interlocked.Increment(ref wasInvoked);
            });

            Assert.Equal(1, Volatile.Read(ref wasInvoked));
            Assert.False(signal.IsCompleted);
        }

        [Fact]
        public async Task AlreadySetSignalCompilerAwaitableWithoutExternalCaller()
        {
            var signal = new Signal();
            signal.Set();
            await signal;
            Assert.False(signal.IsCompleted);
        }

        [Fact]
        public async Task SignalCompilerAwaitableWithExternalCaller()
        {
            var signal = new Signal();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(100);
                signal.Set();
            });
            await signal;
            Assert.False(signal.IsCompleted);
        }

        [Fact]
        public void ResetClearsContinuation()
        {
            var signal = new Signal();
            bool wasInvoked = false;
            signal.OnCompleted(() =>
            {
                signal.GetResult();
                wasInvoked = true;
            });
            signal.Reset();
            signal.Set();
            Assert.False(wasInvoked);
        }

        [Fact]
        public void CallingSetTwiceHasNoBacklog()
        {
            var signal = new Signal();
            signal.Set();
            signal.Set();
            Assert.True(signal.IsCompleted);
            signal.GetResult();
            Assert.False(signal.IsCompleted); // only set "once"
        }
    }
}
