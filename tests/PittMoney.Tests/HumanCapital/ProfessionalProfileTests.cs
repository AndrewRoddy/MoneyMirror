using PittMoney.HumanCapital;

namespace PittMoney.Tests.HumanCapital;

public class ProfessionalProfileTests
{
    [Fact]
    public void Empty_HasNoFabricatedEntries()
    {
        var profile = ProfessionalProfile.Empty;

        Assert.Empty(profile.Education);
        Assert.Empty(profile.Certifications);
        Assert.Empty(profile.Skills);
        Assert.Empty(profile.Experience);
        Assert.Empty(profile.Projects);
        Assert.Empty(profile.Publications);
        Assert.Empty(profile.Awards);
    }
}
