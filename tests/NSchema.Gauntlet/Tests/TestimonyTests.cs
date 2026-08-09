using NSchema.Gauntlet.Model;
using NSchema.Gauntlet.Runner;

namespace NSchema.Gauntlet.Tests;

public sealed class TestimonyTests
{
    private static CatalogFact Fact(string catalog, string identity, string attribute, string value) =>
        new(catalog, identity, attribute, value);

    [Fact]
    public void Differences_TwoAccountsThatAgree_IsEmpty()
    {
        // Act
        var differences = Testimony.Differences(
            [Fact("pg_class", "public.orders", "relkind", "r")],
            [Fact("pg_class", "public.orders", "relkind", "r")]);

        // Assert
        differences.ShouldBeEmpty();
    }

    [Fact]
    public void Differences_AnAttributeWithADifferentValue_IsOneChange()
    {
        // A loss and an unrelated gain is how this reads before pairing, which doubles the report and hides
        // the one thing worth knowing: what it changed to.

        // Act
        var differences = Testimony.Differences(
            [Fact("pg_class", "public.payment", "relkind", "p")],
            [Fact("pg_class", "public.payment", "relkind", "r")]);

        // Assert
        var difference = differences.ShouldHaveSingleItem();
        difference.Catalog.ShouldBe("pg_class");
        difference.Attribute.ShouldBe("relkind");
        difference.Change.ShouldBe(CatalogChange.Changed);
        difference.Occurrences.ShouldHaveSingleItem().Source.ShouldBe("p");
        difference.Occurrences[0].Rebuild.ShouldBe("r");
    }

    [Fact]
    public void Differences_TheSameAttributeAcrossManyObjects_IsOneFinding()
    {
        // The point of the grouping: one gap in a provider fires on everything it touches, and a report that
        // lists every occurrence reads as many failures rather than the single bug it is.

        // Arrange
        CatalogFact[] source =
        [
            Fact("pg_constraint", "public.a.a_fkey", "confdeltype", "r"),
            Fact("pg_constraint", "public.b.b_fkey", "confdeltype", "r"),
            Fact("pg_constraint", "public.c.c_fkey", "confdeltype", "r"),
        ];
        CatalogFact[] rebuild =
        [
            Fact("pg_constraint", "public.a.a_fkey", "confdeltype", "a"),
            Fact("pg_constraint", "public.b.b_fkey", "confdeltype", "a"),
            Fact("pg_constraint", "public.c.c_fkey", "confdeltype", "a"),
        ];

        // Act
        var differences = Testimony.Differences(source, rebuild);

        // Assert
        var difference = differences.ShouldHaveSingleItem();
        difference.Occurrences.Count.ShouldBe(3);
        Testimony.Describe(differences).ShouldContain("on 3 object(s)");
    }

    [Fact]
    public void Differences_AFactOnOneSideOnly_SaysWhichSide()
    {
        // Act
        var differences = Testimony.Differences(
            [Fact("pg_inherits", "public.p1", "parent", "payment")],
            [Fact("pg_class", "public.extra", "relkind", "r")]);

        // Assert
        differences.ShouldContain(d => d.Catalog == "pg_inherits" && d.Change == CatalogChange.MissingFromRebuild);
        differences.ShouldContain(d => d.Catalog == "pg_class" && d.Change == CatalogChange.OnlyInRebuild);
    }

    [Fact]
    public void Differences_OneAddressHoldingSeveralFacts_ComparesThemAsASet()
    {
        // A table inheriting from two parents reports a parent twice under one address, so losing one of them
        // has to count even though the address still exists on both sides.

        // Act
        var differences = Testimony.Differences(
            [Fact("pg_inherits", "public.child", "parent", "a"), Fact("pg_inherits", "public.child", "parent", "b")],
            [Fact("pg_inherits", "public.child", "parent", "a")]);

        // Assert
        var difference = differences.ShouldHaveSingleItem();
        difference.Change.ShouldBe(CatalogChange.Changed);
        difference.Occurrences[0].Source.ShouldBe("[a, b]");
        difference.Occurrences[0].Rebuild.ShouldBe("a");
    }

    [Fact]
    public void Differences_FactsInADifferentOrder_StillAgree()
    {
        // Nothing sorts the facts on the way in, so agreement cannot depend on the order an engine happens
        // to report them in.

        // Act
        var differences = Testimony.Differences(
            [Fact("pg_class", "public.a", "relkind", "r"), Fact("pg_class", "public.b", "relkind", "v")],
            [Fact("pg_class", "public.b", "relkind", "v"), Fact("pg_class", "public.a", "relkind", "r")]);

        // Assert
        differences.ShouldBeEmpty();
    }

    [Fact]
    public void Describe_MoreOccurrencesThanRepresentatives_SaysHowManyItHeldBack()
    {
        // Arrange
        var source = Enumerable.Range(0, 10).Select(i => Fact("pg_class", $"public.t{i}", "relkind", "p")).ToArray();
        var rebuild = Enumerable.Range(0, 10).Select(i => Fact("pg_class", $"public.t{i}", "relkind", "r")).ToArray();

        // Act
        var described = Testimony.Describe(Testimony.Differences(source, rebuild));

        // Assert
        described.ShouldContain("on 10 object(s)");
        described.ShouldContain("... and 7 more");
    }
}
