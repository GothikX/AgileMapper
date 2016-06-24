﻿namespace AgileObjects.AgileMapper.UnitTests.Configuration
{
    using TestClasses;
    using Xunit;

    public class WhenConfiguringNameMatching
    {
        [Fact]
        public void ShouldHandleASingleCustomPrefix()
        {
            using (var mapper = Mapper.CreateNew())
            {
                mapper
                    .WhenMapping
                    .ExpectNamePrefix("_p");

                var source = new { _pValue = "Help!" };
                var result = mapper.Map(source).ToANew<PublicProperty<string>>();

                result.Value.ShouldBe("Help!");
            }
        }

        [Fact]
        public void ShouldHandleMultipleCustomPrefixes()
        {
            using (var mapper = Mapper.CreateNew())
            {
                mapper
                    .WhenMapping
                    .ExpectNamePrefixes("_p", "_f");

                var source = new { _fValue = "Oops!" };
                var result = mapper.Map(source).ToANew<PublicField<string>>();

                result.Value.ShouldBe("Oops!");
            }
        }
    }
}
