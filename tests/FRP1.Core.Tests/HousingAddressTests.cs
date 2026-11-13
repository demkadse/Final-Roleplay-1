using FRP1.Plugin.Integrations;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class HousingAddressTests
{
    internal static HousingAddress House => new(21, HousingDistrict.Mist, 1, HousingPropertyType.House);

    [Theory]
    [InlineData(HousingDistrict.Goblet, 9)]
    [InlineData(HousingDistrict.LavenderBeds, 2)]
    [InlineData(HousingDistrict.Mist, 8)]
    [InlineData(HousingDistrict.Empyreum, 70)]
    [InlineData(HousingDistrict.Shirogane, 111)]
    public void EverySupportedDistrictKeepsTheVerifiedProviderCityId(HousingDistrict district, int expectedCity)
    {
        var address = House with { District = district };
        Assert.True(LifestreamRules.IsValidAddress(address));
        Assert.Equal(expectedCity, LifestreamRules.ToTuple(address).City);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(30, 60, 90)]
    public void BothPropertyTypesAcceptTheInclusiveProductLimits(int ward, int plot, int apartment)
    {
        foreach (var type in new[] { HousingPropertyType.House, HousingPropertyType.Apartment })
            Assert.True(LifestreamRules.IsValidAddress(House with { Ward = ward, Plot = plot, Apartment = apartment, PropertyType = type }));
    }

    public static IEnumerable<object[]> InvalidAddresses => new HousingAddress?[]
    {
        null, House with { WorldId = 0 }, House with { WorldId = -1 }, House with { WorldId = int.MinValue },
        House with { District = (HousingDistrict)0 }, House with { District = (HousingDistrict)int.MaxValue },
        House with { PropertyType = (HousingPropertyType)(-1) }, House with { PropertyType = (HousingPropertyType)2 },
        House with { Ward = 0 }, House with { Ward = 31 }, House with { Ward = int.MaxValue },
        House with { Plot = 0 }, House with { Plot = 61 }, House with { Plot = int.MinValue },
        House with { Apartment = 0 }, House with { Apartment = 91 }, House with { Apartment = int.MaxValue },
    }.Select(address => new object[] { address! });

    [Theory]
    [MemberData(nameof(InvalidAddresses))]
    public void InvalidAddressesFailClosedBeforeAnyTupleCanBeProduced(HousingAddress? address)
    {
        Assert.False(LifestreamRules.IsValidAddress(address));
        if (address is not null)
            Assert.Throws<InvalidDataException>(() => LifestreamRules.ValidateOptions(new(true, address)));
        Assert.Throws<ArgumentException>(() => LifestreamRules.ToTuple(address!));
    }

    [Fact]
    public void HouseTupleDisablesApartmentFieldsAndAllFreeTextAndAliasFields()
    {
        var address = House with { Ward = 30, Plot = 60, Apartment = 90, ApartmentSubdivision = true };
        Assert.Equal(("", 21, 8, 30, 0, 60, 1, false, false, ""), LifestreamRules.ToTuple(address));
        Assert.Equal(House with { Ward = 30, Plot = 60 }, LifestreamRules.ValidateOptions(new(true, address)).Address);
        Assert.Equal(90, address.Apartment);
        Assert.True(address.ApartmentSubdivision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApartmentTupleKeepsSubdivisionAndApartmentButNeutralizesThePlot(bool subdivision)
    {
        var address = House with { PropertyType = HousingPropertyType.Apartment, Plot = 60, Apartment = 90, ApartmentSubdivision = subdivision };
        Assert.Equal(("", 21, 8, 1, 1, 1, 90, subdivision, false, ""), LifestreamRules.ToTuple(address));
        Assert.Equal(address with { Plot = 1 }, LifestreamRules.ValidateOptions(new(true, address)).Address);
    }

    [Fact]
    public void OptionsDefaultOffAndPermitNoDestinationWithoutInventingOne()
    {
        Assert.Equal(new LifestreamOptions(false, null), LifestreamRules.ValidateOptions(new()));
        Assert.Equal(new LifestreamOptions(true, null), LifestreamRules.ValidateOptions(new(true)));
        Assert.Throws<InvalidDataException>(() => LifestreamRules.ValidateOptions(null!));
    }

    public static IEnumerable<object[]> InvalidWorlds => new HousingWorld?[]
    {
        null, new(0, "Synthetic", 1), new(-1, "Synthetic", 1), new(21, "", 1), new(21, "  ", 1),
        new(21, null!, 1), new(21, "Synthetic", 0), new(21, "Synthetic", -1), new(21, "Synthetic", 1, false),
    }.Select(world => new object[] { world! });

    [Theory]
    [MemberData(nameof(InvalidWorlds))]
    public void WorldMustHavePublicFlagRealIdDataCenterAndName(HousingWorld? world) =>
        Assert.False(LifestreamRules.IsPublicWorld(world));

    [Fact]
    public void SavedWorldIdMustMatchACurrentPublicWorldRatherThanAnyNamedRow()
    {
        Assert.True(LifestreamRules.IsPublicWorld(new(21, "Synthetic World", 1)));
        Assert.True(LifestreamRules.WorldExists(House, [new(21, "Synthetic World", 1)]));
        Assert.False(LifestreamRules.WorldExists(House, [new(22, "Synthetic World", 1)]));
        Assert.False(LifestreamRules.WorldExists(House, [new(21, "Synthetic World", 1, false)]));
        Assert.False(LifestreamRules.WorldExists(House, []));
        Assert.False(LifestreamRules.WorldExists(House, null));
    }

    [Theory]
    [InlineData("2.6.0.13", 15, true)]
    [InlineData("2.5.4.18", 15, false)]
    [InlineData("2.6.0.12", 15, false)]
    [InlineData("2.6.0.14", 15, false)]
    [InlineData("2.6.0", 15, false)]
    [InlineData("2.6.0.13", 14, false)]
    [InlineData("2.6.0.13", 16, false)]
    [InlineData(null, 15, false)]
    public void RuntimeContractRequiresExactlyTheVerifiedReleaseAndDalamudApi(string? version, int api, bool expected) =>
        Assert.Equal(expected, LifestreamRules.IsSupportedProvider(version is null ? null : new Version(version), api));
}
