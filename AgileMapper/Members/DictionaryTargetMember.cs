namespace AgileObjects.AgileMapper.Members
{
    using System;
    using System.Collections.Generic;
#if !NET_STANDARD
    using System.Diagnostics.CodeAnalysis;
#endif
    using System.Linq.Expressions;
#if NET_STANDARD
    using System.Reflection;
#endif
    using Extensions;
    using ReadableExpressions.Extensions;

    internal class DictionaryTargetMember : QualifiedMember
    {
        private readonly DictionaryTargetMember _rootDictionaryMember;
        private bool _createDictionaryChildMembers;
        private Expression _key;

        public DictionaryTargetMember(QualifiedMember wrappedTargetMember)
            : base(wrappedTargetMember.MemberChain, wrappedTargetMember)
        {
            var dictionaryTypes = wrappedTargetMember.Type.GetGenericArguments();
            KeyType = dictionaryTypes[0];
            ValueType = dictionaryTypes[1];
            _rootDictionaryMember = this;
            _createDictionaryChildMembers = true;
        }

        private DictionaryTargetMember(
            QualifiedMember matchedTargetMember,
            DictionaryTargetMember rootDictionaryMember)
            : base(matchedTargetMember.MemberChain, matchedTargetMember)
        {
            KeyType = rootDictionaryMember.KeyType;
            ValueType = rootDictionaryMember.ValueType;
            _rootDictionaryMember = rootDictionaryMember;
            _createDictionaryChildMembers = HasObjectEntries || HasSimpleEntries;
        }

        public Type KeyType { get; }

        public Type ValueType { get; }

        public bool HasObjectEntries => ValueType == typeof(object);

        public bool HasSimpleEntries => ValueType.IsSimple();

        public bool HasEnumerableEntries => ValueType.IsEnumerable();

        public bool HasComplexEntries => !HasObjectEntries && ValueType.IsComplex();

        public override Type GetElementType(Type sourceElementType)
        {
            if (HasObjectEntries || HasSimpleEntries)
            {
                return sourceElementType;
            }

            return base.GetElementType(sourceElementType);
        }

        public override bool GuardObjectValuePopulations => true;

        public DictionaryTargetMember Append(ParameterExpression key)
        {
            var memberKey = new DictionaryMemberKey(ValueType, key.Name, this);
            var childMember = Append(memberKey);

            childMember._key = key;

            return childMember;
        }

        public DictionaryTargetMember Append(Type entryDeclaringType, string entryKey)
        {
            var memberKey = new DictionaryMemberKey(entryDeclaringType, entryKey, this);

            return Append(memberKey);
        }

        private DictionaryTargetMember Append(DictionaryMemberKey memberKey)
        {
            var targetEntryMember = GlobalContext.Instance.Cache.GetOrAdd(
                memberKey,
                key =>
                {
                    var member = key.GetDictionaryEntryMember();

                    key.DictionaryMember = null;

                    return member;
                });

            var childMember = Append(targetEntryMember);

            return (DictionaryTargetMember)childMember;
        }

        protected override QualifiedMember CreateChildMember(Member childMember)
        {
            var matchedTargetEntryMember = base.CreateChildMember(childMember);

            if (_createDictionaryChildMembers)
            {
                return new DictionaryTargetMember(matchedTargetEntryMember, _rootDictionaryMember);
            }

            return matchedTargetEntryMember;
        }

        protected override QualifiedMember CreateRuntimeTypedMember(Type runtimeType)
        {
            var runtimeTypedTargetEntryMember = base.CreateRuntimeTypedMember(runtimeType);

            return new DictionaryTargetMember(runtimeTypedTargetEntryMember, _rootDictionaryMember)
            {
                _createDictionaryChildMembers = _createDictionaryChildMembers
            };
        }

        public override Expression GetAccess(Expression instance, IMemberMapperData mapperData)
        {
            if (this == _rootDictionaryMember)
            {
                return base.GetAccess(instance, mapperData);
            }

            if (ReturnNullAccess())
            {
                return Type.ToDefaultExpression();
            }

            return GetIndexAccess(mapperData);
        }

        private bool ReturnNullAccess()
        {
            if (Type == ValueType)
            {
                return false;
            }

            if (Type.IsSimple())
            {
                return false;
            }

            return true;
        }

        private Expression GetIndexAccess(IMemberMapperData mapperData)
        {
            var index = GetKey(mapperData);
            var dictionaryAccess = GetDictionaryAccess(mapperData);
            var indexAccess = dictionaryAccess.GetIndexAccess(index);

            return indexAccess;
        }

        private Expression GetKey(IMemberMapperData mapperData)
            => _key ?? mapperData.GetValueConversion(mapperData.GetTargetMemberDictionaryKey(), KeyType);

        private Expression GetDictionaryAccess(IMemberMapperData mapperData)
        {
            var parentContextAccess = mapperData.GetAppropriateMappingContextAccess(typeof(object), _rootDictionaryMember.Type);

            if (parentContextAccess.NodeType != ExpressionType.Parameter)
            {
                return MemberMapperDataExtensions.GetTargetAccess(parentContextAccess, _rootDictionaryMember.Type);
            }

            var dictionaryMapperData = mapperData;

            while (dictionaryMapperData.TargetMember != _rootDictionaryMember)
            {
                dictionaryMapperData = dictionaryMapperData.Parent;
            }

            return dictionaryMapperData.InstanceVariable;
        }

        public override bool CheckExistingElementValue => !HasObjectEntries && !HasSimpleEntries;

        public override Expression GetHasDefaultValueCheck(IMemberMapperData mapperData)
        {
            ParameterExpression existingValueVariable;

            var tryGetValueCall = GetTryGetValueCall(mapperData, out existingValueVariable);
            var existingValueIsDefault = existingValueVariable.GetIsDefaultComparison();

            var valueMissingOrDefault = Expression.OrElse(Expression.Not(tryGetValueCall), existingValueIsDefault);

            return Expression.Block(new[] { existingValueVariable }, valueMissingOrDefault);
        }

        public override BlockExpression GetAccessChecked(IMemberMapperData mapperData)
        {
            ParameterExpression existingValueVariable;

            var tryGetValueCall = GetTryGetValueCall(mapperData, out existingValueVariable);

            return Expression.Block(new[] { existingValueVariable }, tryGetValueCall);
        }

        private Expression GetTryGetValueCall(IMemberMapperData mapperData, out ParameterExpression valueVariable)
        {
            var dictionaryAccess = GetDictionaryAccess(mapperData);
            var tryGetValueMethod = dictionaryAccess.Type.GetMethod("TryGetValue");
            var index = GetKey(mapperData);
            valueVariable = Expression.Variable(ValueType, "existingValue");

            var tryGetValueCall = Expression.Call(
                dictionaryAccess,
                tryGetValueMethod,
                index,
                valueVariable);

            return tryGetValueCall;
        }

        public override Expression GetPopulation(Expression value, IMemberMapperData mapperData)
        {
            if (mapperData.TargetMember.IsRecursion)
            {
                return value;
            }

            if (this == _rootDictionaryMember)
            {
                return base.GetPopulation(value, mapperData);
            }

            BlockExpression flattening;

            if (ValueIsFlattening(value, out flattening))
            {
                return flattening;
            }

            var indexAccess = GetAccess(mapperData.InstanceVariable, mapperData);
            var convertedValue = mapperData.GetValueConversion(value, ValueType);
            var indexAssignment = indexAccess.AssignTo(convertedValue);

            return indexAssignment;
        }

        private bool ValueIsFlattening(Expression value, out BlockExpression flattening)
        {
            if (!(HasObjectEntries || HasSimpleEntries))
            {
                flattening = null;
                return false;
            }

            ICollection<ParameterExpression> blockParameters;

            if (value.NodeType == ExpressionType.Block)
            {
                flattening = (BlockExpression)value;
                blockParameters = flattening.Variables;
                value = flattening.Expressions[0];
            }
            else
            {
                blockParameters = Enumerable<ParameterExpression>.EmptyArray;
            }

            if (value.NodeType != ExpressionType.Try)
            {
                flattening = null;
                return false;
            }

            flattening = (BlockExpression)((TryExpression)value).Body;
            var flatteningExpressions = GetMappingExpressions(flattening);

            flattening = blockParameters.Any()
                ? Expression.Block(blockParameters, flatteningExpressions)
                : flatteningExpressions.HasOne()
                    ? (BlockExpression)flatteningExpressions[0]
                    : Expression.Block(flatteningExpressions);

            return true;
        }

        private static IList<Expression> GetMappingExpressions(Expression mapping)
        {
            var expressions = new List<Expression>();

            while (mapping.NodeType == ExpressionType.Block)
            {
                var mappingBlock = (BlockExpression)mapping;
                expressions.AddRange(mappingBlock.Expressions);
                expressions.Remove(mappingBlock.Result);

                mapping = mappingBlock.Result;
            }

            return expressions;
        }

        public DictionaryTargetMember WithTypeOf(Member sourceMember)
        {
            if (sourceMember.Type == Type)
            {
                return this;
            }

            return (DictionaryTargetMember)WithType(sourceMember.Type);
        }

        public override void MapCreating(IQualifiedMember sourceMember)
        {
            if (CreateNonDictionaryChildMembers(sourceMember))
            {
                _createDictionaryChildMembers = false;
            }

            base.MapCreating(sourceMember);
        }

        private bool CreateNonDictionaryChildMembers(IQualifiedMember sourceMember)
        {
            // If this DictionaryTargetMember represents an object-typed dictionary 
            // entry and we're mapping from a source of type object, we switch from
            // mapping to flattened entries to mapping entire objects:
            return HasObjectEntries &&
                   LeafMember.IsEnumerableElement() &&
                  (MemberChain[MemberChain.Length - 2] == _rootDictionaryMember.LeafMember) &&
                  (sourceMember.Type == typeof(object));
        }

        #region ExcludeFromCodeCoverage
#if !NET_STANDARD
        [ExcludeFromCodeCoverage]
#endif
        #endregion
        public override string ToString()
        {
            if (LeafMember.IsRoot)
            {
                return base.ToString();
            }

            var path = GetPath().Substring("Target.".Length);

            return "[\"" + path + "\"]: " + Type.GetFriendlyName();
        }

        #region Helper Classes

        private class DictionaryMemberKey
        {
            private readonly Type _entryDeclaringType;
            private readonly Type _entryValueType;
            private readonly string _entryKey;

            public DictionaryMemberKey(
                Type entryDeclaringType,
                string entryKey,
                DictionaryTargetMember dictionaryMember)
            {
                _entryValueType = dictionaryMember.ValueType;
                _entryDeclaringType = entryDeclaringType;
                _entryKey = entryKey;
                DictionaryMember = dictionaryMember;
            }

            public DictionaryTargetMember DictionaryMember { private get; set; }

            public Member GetDictionaryEntryMember()
            {
                var typedTargetMember = (DictionaryTargetMember)DictionaryMember.WithType(_entryDeclaringType);

                return Member.DictionaryEntry(_entryKey, typedTargetMember);
            }

            public override bool Equals(object obj)
            {
                var otherKey = (DictionaryMemberKey)obj;

                // ReSharper disable once PossibleNullReferenceException
                return (otherKey._entryValueType == _entryValueType) &&
                       (otherKey._entryDeclaringType == _entryDeclaringType) &&
                       (otherKey._entryKey == _entryKey);
            }

            #region ExcludeFromCodeCoverage
#if !NET_STANDARD
            [ExcludeFromCodeCoverage]
#endif
            #endregion
            public override int GetHashCode() => 0;
        }

        #endregion
    }
}