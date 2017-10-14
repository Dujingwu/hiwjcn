﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lib.mq
{
    public interface IMessageQueueConsumer<T> : IDisposable
    {
        //
    }

    public interface IMessageQueueProducer
    {
        /// <summary>发送消息</summary>
        /// <param name="to">routingKey</param>
        /// <param name="message">消息</param>
        void Send(string to, object message);
        /// <summary>发送消息</summary>
        /// <param name="to">routingKey</param>
        /// <param name="message">消息</param>
        /// <param name="priority">优先级</param>
        void Send(string to, object message, MessagePriority priority);
        /// <summary>发送消息</summary>
        /// <param name="to">routingKey</param>
        /// <param name="message">消息</param>
        /// <param name="delay">延迟毫秒数</param>
        void Send(string to, object message, long delay);
        /// <summary>发送消息</summary>
        /// <param name="to">routingKey</param>
        /// <param name="message">消息</param>
        /// <param name="priority">优先级</param>
        /// <param name="delay">延迟毫秒数</param>
        void Send(string to, object message, MessagePriority priority, long delay);

        Task SendAsync<T>(string routeKey, T message, MessagePriority? priority = null, TimeSpan? delay = null);
    }

    [Serializable]
    public enum DeliveryModeEnum : byte
    {
        NonPersistent = 1,
        Persistent = 2
    }

    /// <summary>
    /// exchange类型
    /// </summary>
    [Serializable]
    public enum ExchangeTypeEnum : byte
    {
        /// <summary>
        /// 如果routingKey匹配，那么Message就会被传递到相应的queue中。
        /// http://blog.csdn.net/anzhsoft/article/details/19630147
        /// </summary>
        direct = 1,

        /// <summary>
        /// 会向所有响应的queue广播。
        /// http://blog.csdn.net/anzhsoft/article/details/19617305
        /// </summary>
        fanout = 2,

        /// <summary>
        /// 对key进行模式匹配，比如ab.* 可以传递到所有ab.*的queue。* (星号) 代表任意 一个单词；# (hash) 0个或者多个单词。
        /// http://blog.csdn.net/anzhsoft/article/details/19633079
        /// </summary>
        topic = 3,

        headers = 4
    }

    /// <summary>
    /// 优先级
    /// </summary>
    [Serializable]
    public enum MessagePriority : byte
    {
        /// <summary>
        /// 优先级0
        /// </summary>
        None = 0,
        /// <summary>
        /// 优先级1
        /// </summary>
        Lowest = 1,
        /// <summary>
        /// 优先级2
        /// </summary>
        AboveLowest = 2,
        /// <summary>
        /// 优先级3
        /// </summary>
        Low = 3,
        /// <summary>
        /// 优先级4
        /// </summary>
        BelowNormal = 4,
        /// <summary>
        /// 优先级5
        /// </summary>
        Normal = 5,
        /// <summary>
        /// 优先级6
        /// </summary>
        AboveNormal = 6,
        /// <summary>
        /// 优先级7
        /// </summary>
        Hight = 7,
        /// <summary>
        /// 优先级8
        /// </summary>
        BelowHighest = 8,
        /// <summary>
        /// 优先级9
        /// </summary>
        Highest = 9
    }
}
