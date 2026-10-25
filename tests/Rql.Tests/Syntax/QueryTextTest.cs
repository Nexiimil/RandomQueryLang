using Rql.Syntax;

namespace Rql.Tests.Syntax;

public class QueryTextTest
{
    [Theory]
    [InlineData("loot", "loot")]
    [InlineData("Weapon_Types", "Weapon_Types")]
    [InlineData("Weapon Types", "\"Weapon Types\"")]
    [InlineData("d6", "\"d6\"")]
    [InlineData("Monsters.Undead", "Monsters.Undead")]
    [InlineData("Gear.Weapon Types", "\"Gear.Weapon Types\"")]
    [InlineData("4th Level", "\"4th Level\"")]
    [InlineData("Say \"hi\"\\", "\"Say \\\"hi\\\"\\\\\"")]
    public void TablesAreReferredToSoTheyReadBack(string name, string written)
    {
        Assert.Equal(written, QueryText.TableReference(name));
    }

    [Theory]
    [InlineData("loot")]
    [InlineData("Monsters.Undead")]
    [InlineData("Weapon Types")]
    [InlineData("d6")]
    [InlineData("Say \"hi\"\\")]
    public void AReferenceParsesBackToTheName(string name)
    {
        Expr parsed = Parser.Parse(QueryText.TableReference(name));

        Assert.True(parsed is Identifier { Name: var bare } && bare == name
            || parsed is TextLiteral { Value: var quoted } && quoted == name);
    }
}
