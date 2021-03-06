namespace AgileObjects.AgileMapper
{
    using DataSources;
    using Members.Population;
    using ObjectPopulation.Enumerables;

    internal class MappingRuleSet
    {
        public MappingRuleSet(
            string name,
            bool rootHasPopulatedTarget,
            IEnumerablePopulationStrategy enumerablePopulationStrategy,
            IMemberPopulationGuardFactory populationGuardFactory,
            IDataSourceFactory fallbackDataSourceFactory)
        {
            Name = name;
            RootHasPopulatedTarget = rootHasPopulatedTarget;
            EnumerablePopulationStrategy = enumerablePopulationStrategy;
            PopulationGuardFactory = populationGuardFactory;
            FallbackDataSourceFactory = fallbackDataSourceFactory;
        }

        public string Name { get; }

        public bool RootHasPopulatedTarget { get; }

        public IEnumerablePopulationStrategy EnumerablePopulationStrategy { get; }

        public IMemberPopulationGuardFactory PopulationGuardFactory { get; }

        public IDataSourceFactory FallbackDataSourceFactory { get; }
    }
}