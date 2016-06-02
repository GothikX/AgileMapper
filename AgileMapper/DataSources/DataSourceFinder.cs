﻿namespace AgileObjects.AgileMapper.DataSources
{
    using System.Collections.Generic;
    using System.Linq;
    using Members;
    using ObjectPopulation;

    internal class DataSourceFinder
    {
        public DataSourceSet FindFor(IMemberMappingContext context)
            => new DataSourceSet(EnumerateSuccessfulDataSources(context).ToArray());

        public IDataSource DataSourceAt(int index, IMemberMappingContext context)
            => EnumerateSuccessfulDataSources(context).ElementAt(index);

        private IEnumerable<IDataSource> EnumerateSuccessfulDataSources(IMemberMappingContext context)
            => EnumerateDataSources(context).Where(dataSource => dataSource.IsValid);

        private IEnumerable<IDataSource> EnumerateDataSources(IMemberMappingContext context)
        {
            if (context.Parent == null)
            {
                yield return RootSourceMemberDataSourceFor(context);
                yield break;
            }

            var dataSourceIndex = 0;

            IEnumerable<IConfiguredDataSource> configuredDataSources;

            if (DataSourcesAreConfigured(context, out configuredDataSources))
            {
                foreach (var configuredDataSource in configuredDataSources)
                {
                    yield return GetFinalDataSource(configuredDataSource, dataSourceIndex, context);

                    if (!configuredDataSource.IsConditional)
                    {
                        yield break;
                    }

                    ++dataSourceIndex;
                }
            }

            if (context.TargetMember.IsComplex)
            {
                var complexTypeDataSource = new ComplexTypeMappingDataSource(context.SourceMember, dataSourceIndex, context);
                yield return complexTypeDataSource;

                if (complexTypeDataSource.IsConditional)
                {
                    yield return FallbackDataSourceFor(context);
                }

                yield break;
            }

            var matchingSourceMemberDataSource = GetSourceMemberDataSourceOrNull(context);

            if ((matchingSourceMemberDataSource == null) ||
                configuredDataSources.Any(cds => cds.IsSameAs(matchingSourceMemberDataSource)))
            {
                yield return FallbackDataSourceFor(context);
                yield break;
            }

            yield return matchingSourceMemberDataSource;

            if (matchingSourceMemberDataSource.IsConditional)
            {
                yield return FallbackDataSourceFor(context);
            }
        }

        private static IDataSource RootSourceMemberDataSourceFor(IMemberMappingContext context)
            => GetSourceMemberDataSourceFor(QualifiedMember.From(Member.RootSource(context.SourceObject.Type)), 0, context);

        private static bool DataSourcesAreConfigured(IMemberMappingContext context, out IEnumerable<IConfiguredDataSource> configuredDataSources)
        {
            configuredDataSources = context
                .MapperContext
                .UserConfigurations
                .GetDataSources(context);

            return configuredDataSources.Any();
        }

        private static IDataSource FallbackDataSourceFor(IMemberMappingContext context)
            => context.MappingContext.RuleSet.FallbackDataSourceFactory.Create(context);

        public IDataSource GetSourceMemberDataSourceOrNull(IMemberMappingContext context)
        {
            if (context.Parent == null)
            {
                return RootSourceMemberDataSourceFor(context);
            }

            return GetSourceMemberDataSourceOrNull(0, context);
        }

        private IDataSource GetSourceMemberDataSourceOrNull(int dataSourceIndex, IMemberMappingContext context)
        {
            var bestMatchingSourceMember = GetSourceMemberFor(context);

            if (bestMatchingSourceMember == null)
            {
                return null;
            }

            bestMatchingSourceMember = bestMatchingSourceMember.RelativeTo(context.SourceObjectDepth);

            return GetSourceMemberDataSourceFor(bestMatchingSourceMember, dataSourceIndex, context);
        }

        private static IDataSource GetSourceMemberDataSourceFor(
            IQualifiedMember sourceMember,
            int dataSourceIndex,
            IMemberMappingContext context)
        {
            return GetFinalDataSource(
                new SourceMemberDataSource(sourceMember, context),
                dataSourceIndex,
                context);
        }

        private static IDataSource GetFinalDataSource(
            IDataSource foundDataSource,
            int dataSourceIndex,
            IMemberMappingContext context)
        {
            if (context.TargetMember.IsEnumerable)
            {
                return new EnumerableMappingDataSource(foundDataSource, dataSourceIndex, context);
            }

            return foundDataSource;
        }

        public IQualifiedMember GetSourceMemberFor(IMemberMappingContext context)
        {
            var rootSourceMember = context.SourceMember;

            return GetAllSourceMembers(rootSourceMember, context.Parent)
                .FirstOrDefault(sm => IsMatchingMember(sm, context));
        }

        private static bool IsMatchingMember(IQualifiedMember sourceMember, IMemberMappingContext context)
        {
            return sourceMember.Matches(context.TargetMember) &&
                   context.MapperContext.ValueConverters.CanConvert(sourceMember.Type, context.TargetMember.Type);
        }

        private static IEnumerable<IQualifiedMember> GetAllSourceMembers(
            IQualifiedMember parentMember,
            IObjectMappingContext currentOmc)
        {
            yield return parentMember;

            var parentMemberType = currentOmc.GetSourceMemberRuntimeType(parentMember);

            if (parentMemberType != parentMember.Type)
            {
                parentMember = parentMember.WithType(parentMemberType);
                yield return parentMember;
            }

            foreach (var sourceMember in currentOmc.GlobalContext.MemberFinder.GetReadableMembers(parentMember.Type))
            {
                var childMember = parentMember.Append(sourceMember);

                if (sourceMember.IsSimple)
                {
                    yield return childMember;
                    continue;
                }

                foreach (var qualifiedMember in GetAllSourceMembers(childMember, currentOmc))
                {
                    yield return qualifiedMember;
                }
            }
        }
    }
}