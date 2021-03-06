﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Channels
{
    public class ChannelFactory : IDisposable
    {
        private readonly MemoryPool _pool;

        public ChannelFactory() : this(new MemoryPool())
        {
        }

        public ChannelFactory(MemoryPool pool)
        {
            _pool = pool;
        }

        public Channel CreateChannel() => new Channel(_pool);

        public IReadableChannel MakeReadableChannel(Stream stream)
        {
            if (!stream.CanRead)
            {
                throw new InvalidOperationException();
            }

            var channel = new Channel(_pool);
            ExecuteCopyToAsync(channel, stream);
            return channel;
        }

        private async void ExecuteCopyToAsync(Channel channel, Stream stream)
        {
            await channel.ReadingStarted;

            await stream.CopyToAsync(channel);
        }

        public IWritableChannel MakeWriteableChannel(Stream stream)
        {
            if (!stream.CanWrite)
            {
                throw new InvalidOperationException();
            }

            var channel = new Channel(_pool);

            channel.CopyToAsync(stream).ContinueWith((task) =>
            {
                if (task.IsFaulted)
                {
                    channel.CompleteReading(task.Exception);
                }
                else
                {
                    channel.CompleteReading();
                }
            });

            return channel;
        }

        public IWritableChannel MakeWriteableChannel(IWritableChannel channel, Func<IReadableChannel, IWritableChannel, Task> consume)
        {
            var newChannel = new Channel(_pool);

            consume(newChannel, channel).ContinueWith(t =>
            {
            });

            return newChannel;
        }

        public IReadableChannel MakeReadableChannel(IReadableChannel channel, Func<IReadableChannel, IWritableChannel, Task> produce)
        {
            var newChannel = new Channel(_pool);
            Execute(channel, newChannel, produce);
            return newChannel;
        }

        private async void Execute(IReadableChannel channel, Channel newChannel, Func<IReadableChannel, IWritableChannel, Task> produce)
        {
            await newChannel.ReadingStarted;

            await produce(channel, newChannel);
        }

        public void Dispose() => _pool.Dispose();
    }
}
