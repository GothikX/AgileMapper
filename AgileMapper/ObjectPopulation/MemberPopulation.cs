namespace AgileObjects.AgileMapper.ObjectPopulation
{
    using System.Linq.Expressions;

    internal class MemberPopulation
    {
        private static readonly Expression _emptyExpression = Expression.Empty();

        public static MemberPopulation Empty = new MemberPopulation(_emptyExpression, _emptyExpression, null);

        public MemberPopulation(
            Expression value,
            Expression population,
            IObjectMappingContext omc)
        {
            Value = value;
            Population = population;
            ObjectMappingContext = omc;
        }

        public Expression Value { get; }

        public Expression Population { get; }

        public IObjectMappingContext ObjectMappingContext { get; }

        public bool IsSuccessful => Population != _emptyExpression;

        public MemberPopulation WithPopulation(Expression updatedPopulation)
        {
            return new MemberPopulation(Value, updatedPopulation, ObjectMappingContext);
        }
    }
}