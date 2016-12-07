namespace AgileObjects.AgileMapper.Members
{
    using System.Linq.Expressions;
    using DataSources;
    using Extensions;

    internal class PreserveExistingValueDataSourceFactory : IDataSourceFactory
    {
        public static readonly IDataSourceFactory Instance = new PreserveExistingValueDataSourceFactory();

        public IDataSource Create(IMemberMapperData mapperData)
            => new PreserveExistingValueDataSource(mapperData);

        private class PreserveExistingValueDataSource : DataSourceBase
        {
            public PreserveExistingValueDataSource(IMemberMapperData mapperData)
                : this(
                      mapperData.SourceMember,
                      mapperData.TargetMember.IsReadable
                          ? mapperData.GetTargetMemberAccess()
                          : Constants.EmptyExpression)
            {
            }

            private PreserveExistingValueDataSource(
                IQualifiedMember sourceMember,
                Expression value)
                : base(
                      sourceMember,
                      Enumerable<ParameterExpression>.Empty,
                      value,
                      (value != Constants.EmptyExpression)
                        ? value.GetIsNotDefaultComparison()
                        : Constants.EmptyExpression)
            {
            }
        }
    }
}