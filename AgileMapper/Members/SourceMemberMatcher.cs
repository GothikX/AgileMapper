﻿namespace AgileObjects.AgileMapper.Members
{
    using System.Collections.Generic;
    using System.Linq;

    internal static class SourceMemberMatcher
    {
        public static IQualifiedMember GetMatchFor(IChildMemberMappingData rootData)
        {
            var rootSourceMember = rootData.MapperData.SourceMember;

            var matchingMember = GetAllSourceMembers(rootSourceMember, rootData)
                .FirstOrDefault(sm => IsMatchingMember(sm, rootData.MapperData));

            if (matchingMember == null)
            {
                return null;
            }

            return rootData.MapperData
                .MapperContext
                .QualifiedMemberFactory
                .GetFinalSourceMember(matchingMember, rootData.MapperData.TargetMember);
        }

        private static IEnumerable<IQualifiedMember> GetAllSourceMembers(
            IQualifiedMember parentMember,
            IChildMemberMappingData rootData)
        {
            yield return parentMember;

            if (!parentMember.CouldMatch(rootData.MapperData.TargetMember))
            {
                yield break;
            }

            var parentMemberType = rootData.GetSourceMemberRuntimeType(parentMember);

            if (parentMemberType != parentMember.Type)
            {
                parentMember = parentMember.WithType(parentMemberType);
                yield return parentMember;
            }

            var relevantSourceMembers = GlobalContext
                .Instance
                .MemberFinder
                .GetSourceMembers(parentMember.Type)
                .Where(sourceMember => MembersHaveCompatibleTypes(sourceMember, rootData));

            foreach (var sourceMember in relevantSourceMembers)
            {
                var childMember = parentMember.Append(sourceMember);

                if (sourceMember.IsSimple)
                {
                    yield return childMember;
                    continue;
                }

                foreach (var qualifiedMember in GetAllSourceMembers(childMember, rootData))
                {
                    yield return qualifiedMember;
                }
            }
        }

        private static bool MembersHaveCompatibleTypes(Member sourceMember, IChildMemberMappingData rootData)
        {
            if (!sourceMember.IsSimple)
            {
                return true;
            }

            var targetMember = rootData.MapperData.TargetMember;

            if (targetMember.IsSimple)
            {
                return true;
            }

            return targetMember.Type == typeof(object);
        }

        private static bool IsMatchingMember(IQualifiedMember sourceMember, IMemberMapperData mapperData)
        {
            return sourceMember.Matches(mapperData.TargetMember) &&
                   mapperData.MapperContext.ValueConverters.CanConvert(sourceMember.Type, mapperData.TargetMember.Type);
        }
    }
}