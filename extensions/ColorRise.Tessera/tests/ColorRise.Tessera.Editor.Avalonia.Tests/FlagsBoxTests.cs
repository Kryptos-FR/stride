using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ColorRise.Tessera.Editor.Avalonia.Controls;
using ColorRise.Tessera.Editor.Avalonia.Views;
using Stride.Core.Assets.Editor.Avalonia.Views;
using Stride.Core.Presentation.Quantum.ViewModels;

namespace ColorRise.Tessera.Editor.Avalonia.Tests;

/// <summary>
/// Tessera's editor of its flags in the property grid, a check box for each flag, which Game Studio's own editor of flags
/// is not: its boxes show nothing and write nothing (studio check, step 3, line 7).
/// </summary>
public sealed class FlagsBoxTests
{
    [AvaloniaFact]
    public void It_has_a_check_box_for_each_flag_ticked_as_the_value()
    {
        var box = new FlagsBox { EnumType = typeof(TileFlip), Value = TileFlip.Vertical };

        var checks = Checks(box);

        Assert.Equal(["Horizontal", "Vertical", "Diagonal"], checks.Select(check => check.Content));
        Assert.Equal([false, true, false], checks.Select(check => check.IsChecked));
        Assert.All(checks, check => Assert.True(check.IsEnabled));
    }

    [AvaloniaFact]
    public void A_ticked_box_sets_its_flag_and_keeps_the_others()
    {
        var box = new FlagsBox { EnumType = typeof(TileFlip), Value = TileFlip.Vertical };

        Checks(box)[0].IsChecked = true;
        Assert.Equal(TileFlip.Horizontal | TileFlip.Vertical, box.Value);

        Checks(box)[1].IsChecked = false;
        Assert.Equal(TileFlip.Horizontal, box.Value);
    }

    [AvaloniaFact]
    public void A_new_value_ticks_its_boxes_and_writes_nothing_back()
    {
        var box = new FlagsBox { EnumType = typeof(TileFlip), Value = TileFlip.Vertical };
        var written = 0;
        box.PropertyChanged += (_, e) => written += e.Property == FlagsBox.ValueProperty ? 1 : 0;

        box.Value = TileFlip.Horizontal | TileFlip.Diagonal;

        Assert.Equal([true, false, true], Checks(box).Select(check => check.IsChecked));
        Assert.Equal(1, written);
        Assert.Equal(TileFlip.Horizontal | TileFlip.Diagonal, box.Value);
    }

    [AvaloniaFact]
    public void A_nullable_flags_type_has_its_boxes_too()
    {
        var box = new FlagsBox { EnumType = typeof(TileCollision?), Value = TileCollision.None };

        var check = Assert.Single(Checks(box));
        Assert.Equal("Solid", check.Content);
        Assert.False(check.IsChecked);

        check.IsChecked = true;
        Assert.Equal(TileCollision.Solid, box.Value);
    }

    [AvaloniaFact]
    public void Different_values_tick_nothing_and_cannot_be_changed()
    {
        var box = new FlagsBox { EnumType = typeof(TileFlip), Value = NodeViewModel.DifferentValues };

        Assert.All(Checks(box), check =>
        {
            Assert.Null(check.IsChecked);
            Assert.False(check.IsEnabled);
        });
    }

    [AvaloniaFact]
    public void The_property_grid_uses_the_box_for_both_flags_types()
    {
        var providers = new TesseraPropertyTemplateProviders().Values.OfType<TypeMatchTemplateProvider>().ToList();

        Assert.Equal(new HashSet<Type?> { typeof(TileFlip), typeof(TileCollision) }, providers.Select(provider => provider.Type).ToHashSet());
        Assert.All(providers, provider =>
        {
            Assert.Equal(PropertyViewHelper.Category.PropertyEditor, PropertyViewHelper.GetTemplateCategory(provider));
            Assert.Contains("Enum", provider.OverriddenProviderNames);
            Assert.Contains("FlagEnum", provider.OverriddenProviderNames);
            Assert.IsType<FlagsBox>(provider.Template.Build(null));
        });
    }

    private static List<CheckBox> Checks(FlagsBox box) => box.Children.OfType<CheckBox>().ToList();
}
