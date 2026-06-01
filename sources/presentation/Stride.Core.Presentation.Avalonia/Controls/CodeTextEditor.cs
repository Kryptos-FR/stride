// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Avalonia;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace Stride.Core.Presentation.Avalonia.Controls;

/// <summary>
/// A reusable code editor control based on AvaloniaEdit, providing TextMate-based C# syntax
/// highlighting that follows the application's light/dark theme.
/// </summary>
public class CodeTextEditor : TextEditor
{
    private readonly RegistryOptions registryOptions;
    private readonly TextMate.Installation textMate;
    private bool suppressTextChanged;

    /// <summary>
    /// Occurs when the user edits the text. It is not raised for programmatic <see cref="LoadText"/> calls.
    /// </summary>
    public event EventHandler<string>? TextEdited;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeTextEditor"/> class.
    /// </summary>
    public CodeTextEditor()
    {
        ShowLineNumbers = true;
        WordWrap = false;
        registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        textMate = this.InstallTextMate(registryOptions);
        textMate.SetGrammar(registryOptions.GetScopeByLanguageId(registryOptions.GetLanguageByExtension(".cs").Id));
        TextChanged += OnTextChanged;
    }

    /// <summary>
    /// Loads the given text into the editor without raising <see cref="TextEdited"/> and resets the undo history.
    /// </summary>
    /// <param name="text">The text to display.</param>
    public void LoadText(string text)
    {
        suppressTextChanged = true;
        try
        {
            Document.Text = text ?? string.Empty;
            Document.UndoStack.ClearAll();
        }
        finally
        {
            suppressTextChanged = false;
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyThemeVariant(ActualThemeVariant);
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        ApplyThemeVariant(ActualThemeVariant);
    }

    private void ApplyThemeVariant(ThemeVariant variant)
    {
        var themeName = variant == ThemeVariant.Light ? ThemeName.LightPlus : ThemeName.DarkPlus;
        textMate.SetTheme(registryOptions.LoadTheme(themeName));
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (!suppressTextChanged)
            TextEdited?.Invoke(this, Text);
    }
}
