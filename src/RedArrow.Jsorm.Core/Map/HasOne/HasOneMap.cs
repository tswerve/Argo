﻿using RedArrow.Jsorm.Core.Map.MapAttributes;
using RedArrow.Jsorm.Core.Session;
using System;
using System.Linq.Expressions;

namespace RedArrow.Jsorm.Core.Map.HasOne
{
    public class HasOneMap<TModel, TProp> :
        PropertyMap<TModel, TProp>,
        IHasOneMap<TModel, TProp>
        where TProp : new()
    {
        public HasOneMap(Expression<Func<TModel, TProp>> property, string attrName = null) :
            base(property, attrName)
        {
        }

        public override void Configure(ISessionFactory factory)
        {
            throw new NotImplementedException();
        }

        public IHasOneMap<TModel, TProp> Lazy()
        {
            MapAttributes[nameof(FetchStrategyAttribute)] = FetchStrategyAttribute.Lazy;
            return this;
        }

        public IHasOneMap<TModel, TProp> Eager()
        {
            MapAttributes[nameof(FetchStrategyAttribute)] = FetchStrategyAttribute.Eager;
            return this;
        }

        public IHasOneMap<TModel, TProp> CascadeDelete()
        {
            throw new NotImplementedException();
        }
    }
}