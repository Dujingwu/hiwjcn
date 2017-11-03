﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka;
using Akka.Actor;
using Lib.core;
using Lib.helper;
using Lib.extension;
using System.Threading;

namespace Lib.actor
{
    public class AkkaSystemManager : StaticClientManager<ActorSystem>
    {
        public static readonly AkkaSystemManager Instance = new AkkaSystemManager();

        public override string DefaultKey => "DefaultActorSystem";

        public override bool CheckClient(ActorSystem ins)
        {
            return ins != null;
        }

        public override ActorSystem CreateNewClient(string key)
        {
            return ActorSystem.Create(key);
        }

        public override void DisposeClient(ActorSystem ins)
        {
            ins?.Dispose();
        }
    }

    public class ActorsManager<T> : StaticClientManager<IActorRef> where T : ActorBase, new()
    {
        public static readonly ActorsManager<T> Instance = new ActorsManager<T>();

        public override string DefaultKey => string.Empty;

        public override bool CheckClient(IActorRef ins)
        {
            return ins != null;
        }

        public override IActorRef CreateNewClient(string key)
        {
            $"Create a actor:{typeof(T).FullName},name:{key}".AddBusinessInfoLog();
            return AkkaHelper<T>.GetActor(key);
        }

        public override void DisposeClient(IActorRef ins)
        {
            ins?.FeedPoisonPill();
        }
    }

    public class ActorAndName
    {
        public ActorAndName(Type t, string n)
        {
            this.ActorType = t;
            this.Name = n;
        }

        public Type ActorType { get; private set; }
        public string Name { get; private set; }
    }

    public class ActorsManager : StaticClientManager<IActorRef, ActorAndName>
    {
        public override ActorAndName DefaultKey => throw new NotImplementedException();

        public override string CreateStringKey(ActorAndName key)
        {
            return $"{key.ActorType.FullName}.name={key.Name}";
        }

        public override bool CheckClient(IActorRef ins)
        {
            return ins != null;
        }

        public override IActorRef CreateNewClient(ActorAndName key)
        {
            return AkkaHelper.GetActor(key.ActorType, key.Name);
        }

        public override void DisposeClient(IActorRef ins)
        {
            ins?.FeedPoisonPill();
        }
    }

    public static class AkkaHelper
    {
        public static ActorSystem Container => AkkaSystemManager.Instance.DefaultClient;

        public static IActorRef GetActor<T>(string name = null) where T : ActorBase, new()
        {
            if (ValidateHelper.IsPlumpString(name))
            {
                return Container.ActorOf<T>(name);
            }
            else
            {
                return Container.ActorOf<T>();
            }
        }

        public static IActorRef GetActor(Type t, string name = null)
        {
            if (ValidateHelper.IsPlumpString(name))
            {
                return Container.ActorOf(Props.Create(t), name);
            }
            else
            {
                return Container.ActorOf(Props.Create(t));
            }
        }
    }

    public static class AkkaHelper<T> where T : ActorBase, new()
    {
        public static IActorRef GetActor(string name = null) => AkkaHelper.GetActor<T>(name);

        [Obsolete("每次都会创建新的actor")]
        public static void Tell(object data, string actor_name = null)
        {
            GetActor(actor_name).Tell(data);
        }

        [Obsolete("每次都会创建新的actor")]
        public static async Task<Answer> Ask<Answer>(object data, string actor_name = null,
            TimeSpan? timeout = null, CancellationToken? cancellationToken = null)
        {
            var actor = GetActor(actor_name);
            if (timeout != null && cancellationToken != null)
            {
                return await actor.Ask<Answer>(data, timeout.Value, cancellationToken.Value);
            }
            if (timeout != null)
            {
                return await actor.Ask<Answer>(data, timeout: timeout.Value);
            }
            if (cancellationToken != null)
            {
                return await actor.Ask<Answer>(data, cancellationToken: cancellationToken.Value);
            }

            return await GetActor(actor_name).Ask<Answer>(data);
        }
    }

    public class KillableReceiveActor : ReceiveActor
    {
        public KillableReceiveActor()
        {
            this.Receive<ActorPoisonPill>(x =>
            {
                Context.Stop(Self);
            });
        }

        protected override void PostStop()
        {
            $"{this.GetType()}stoped".AddBusinessInfoLog();
            base.PostStop();
        }
    }

    public class ActorPoisonPill
    {
        public static readonly ActorPoisonPill Instance = new ActorPoisonPill();
    }

    public static class ActorExtension
    {
        /// <summary>
        /// 通知停止actor
        /// </summary>
        public static void FeedPoisonPill(this IActorRef actor)
        {
            actor.Tell(ActorPoisonPill.Instance);
        }
    }
}
