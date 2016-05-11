﻿namespace AgileObjects.AgileMapper.Api.Configuration
{
    using ObjectPopulation;

    public class PreEventMappingConfigStartingPoint<TSource, TTarget>
    {
        private readonly MappingConfigInfo _configInfo;

        internal PreEventMappingConfigStartingPoint(MappingConfigInfo configInfo)
        {
            _configInfo = configInfo;
        }

        public InstanceCreationCallbackSpecifier<TSource, TTarget, object> CreatingInstances
            => CreateCallbackSpecifier<object>();

        public InstanceCreationCallbackSpecifier<TSource, TTarget, TTarget> CreatingTargetInstances
            => CreateCallbackSpecifier<TTarget>();

        public InstanceCreationCallbackSpecifier<TSource, TTarget, TInstance> CreatingInstancesOf<TInstance>() where TInstance : class
            => CreateCallbackSpecifier<TInstance>();

        private InstanceCreationCallbackSpecifier<TSource, TTarget, TInstance> CreateCallbackSpecifier<TInstance>()
            => new InstanceCreationCallbackSpecifier<TSource, TTarget, TInstance>(CallbackPosition.Before, _configInfo);
    }
}