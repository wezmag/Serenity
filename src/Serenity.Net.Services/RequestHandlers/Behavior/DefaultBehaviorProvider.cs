﻿using System.Collections;

namespace Serenity.Services
{
    public class DefaultBehaviorProvider : IBehaviorProvider
    {
        private readonly IImplicitBehaviorRegistry implicitBehaviors;
        private readonly IBehaviorFactory behaviorFactory;

        public DefaultBehaviorProvider(IImplicitBehaviorRegistry implicitBehaviors,
            IBehaviorFactory behaviorFactory)
        {
            this.implicitBehaviors = implicitBehaviors ??
                throw new ArgumentNullException(nameof(implicitBehaviors));

            this.behaviorFactory = behaviorFactory ??
                throw new ArgumentNullException(nameof(behaviorFactory));
        }

        public IEnumerable Resolve(Type handlerType, Type rowType, Type behaviorType)
        {
            var list = new List<object>();

            var row = (IRow)Activator.CreateInstance(rowType);

            foreach (var type in implicitBehaviors.GetTypes())
            {
                if (!behaviorType.IsAssignableFrom(type))
                    continue;

                var behavior = behaviorFactory.CreateInstance(type);
                if (behavior == null)
                    continue;

                if (!(behavior is IImplicitBehavior implicitBehavior))
                    continue;

                if (!(behavior is IFieldBehavior fieldBehavior))
                {
                    if (implicitBehavior.ActivateFor(row))
                        list.Add(behavior);

                    continue;
                }

                foreach (var field in row.GetFields())
                {
                    (behavior as IFieldBehavior).Target = field;
                    if (implicitBehavior.ActivateFor(row))
                    {
                        list.Add(behavior);

                        behavior = behaviorFactory.CreateInstance(type);
                        implicitBehavior = behavior as IImplicitBehavior;
                        fieldBehavior = behavior as IFieldBehavior;
                    }
                }
            }

            foreach (var attr in row.GetType().GetCustomAttributes<AddBehaviorAttribute>())
            {
                if (behaviorType.IsAssignableFrom(attr.Value))
                    list.Add(behaviorFactory.CreateInstance(attr.Value));
            }

            foreach (var field in row.GetFields())
            {
                foreach (var attr in field.CustomAttributes.OfType<AddBehaviorAttribute>())
                {
                    if (behaviorType.IsAssignableFrom(attr.Value) &&
                        typeof(IFieldBehavior).IsAssignableFrom(attr.Value))
                    {
                        var fieldBehavior = (IFieldBehavior)behaviorFactory.CreateInstance(attr.Value);
                        fieldBehavior.Target = field;
                        list.Add(fieldBehavior);
                    }
                }
            }

            return list;
        }
    }
}