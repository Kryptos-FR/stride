// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace Stride.AvaloniaUI.Sample.ViewModels;

/// <summary>
/// Where the game is: what the views show and which input the game takes.
/// </summary>
public enum GameState
{
    MainMenu,
    Playing,
    Paused,
    GameOver,
}

/// <summary>
/// A line of the best scores.
/// </summary>
/// <param name="IsLatest">Whether it is the score of the round that just ended.</param>
public sealed record ScoreEntry(int Rank, string Name, int Score, bool IsLatest);

/// <summary>
/// A life of the player, shown in the HUD.
/// </summary>
public sealed record LifeSlot(bool IsLost);

/// <summary>
/// The state of the game that the XAML views bind to. The game (<see cref="Gameplay.OrbField"/>) plays the rounds and
/// updates it; the views show it and change it through their controls and commands.
/// </summary>
public sealed class GameViewModel : ObservableObject
{
    public const int MaxLives = 3;
    private const int BestScoreCount = 5;

    private readonly List<(string Name, int Score)> scores = [];

    public GameViewModel()
    {
        PlayCommand = new RelayCommand(StartRound);
        PauseCommand = new RelayCommand(() => { if (State == GameState.Playing) State = GameState.Paused; });
        ResumeCommand = new RelayCommand(() => { if (State == GameState.Paused) State = GameState.Playing; });
        MenuCommand = new RelayCommand(() => State = GameState.MainMenu);
        QuitCommand = new RelayCommand(() => QuitRequested?.Invoke());
    }

    /// <summary>
    /// Raised when a round starts, for the game to clear the field.
    /// </summary>
    public event Action? RoundStarted;

    /// <summary>
    /// Raised when the player quits the game.
    /// </summary>
    public event Action? QuitRequested;

    public GameState State
    {
        get;
        set
        {
            if (!SetField(ref field, value))
                return;

            OnPropertyChanged(nameof(IsMainMenu));
            OnPropertyChanged(nameof(IsPaused));
            OnPropertyChanged(nameof(IsGameOver));
        }
    }

    public bool IsMainMenu => State == GameState.MainMenu;

    public bool IsPaused => State == GameState.Paused;

    public bool IsGameOver => State == GameState.GameOver;

    // Settings of the main menu

    public string PlayerName { get; set => SetField(ref field, value); } = "Player";

    /// <summary>
    /// Gets or sets how fast the orbs move and come, from 0.5 to 2.
    /// </summary>
    public double OrbSpeed { get; set => SetField(ref field, value); } = 1;

    /// <summary>
    /// Gets or sets whether red orbs come, which cost a life.
    /// </summary>
    public bool BombsEnabled { get; set => SetField(ref field, value); } = true;

    /// <summary>
    /// Gets or sets what draws the UI, shown under the main menu.
    /// </summary>
    public string RendererName { get; set => SetField(ref field, value); } = string.Empty;

    // The round

    public double RoundLength => 45;

    public int Score { get; set => SetField(ref field, value); }

    public int Lives
    {
        get;
        set
        {
            if (SetField(ref field, value))
                OnPropertyChanged(nameof(LifeSlots));
        }
    } = MaxLives;

    public IReadOnlyList<LifeSlot> LifeSlots => [.. Enumerable.Range(0, MaxLives).Select(i => new LifeSlot(i >= Lives))];

    /// <summary>
    /// Gets or sets the seconds left in the round.
    /// </summary>
    public double TimeLeft
    {
        get;
        set
        {
            if (SetField(ref field, value))
                OnPropertyChanged(nameof(TimeText));
        }
    }

    public string TimeText => $"{Math.Ceiling(TimeLeft):0}";

    public string GameOverTitle { get; private set => SetField(ref field, value); } = string.Empty;

    public ObservableCollection<ScoreEntry> BestScores { get; } = [];

    // What the buttons do

    public ICommand PlayCommand { get; }

    public ICommand PauseCommand { get; }

    public ICommand ResumeCommand { get; }

    public ICommand MenuCommand { get; }

    public ICommand QuitCommand { get; }

    /// <summary>
    /// Pauses or resumes the round, for the game's pause key.
    /// </summary>
    public void TogglePause()
    {
        if (State == GameState.Playing)
            State = GameState.Paused;
        else if (State == GameState.Paused)
            State = GameState.Playing;
    }

    /// <summary>
    /// Ends the round and adds its score to the best ones.
    /// </summary>
    public void EndRound()
    {
        var name = string.IsNullOrWhiteSpace(PlayerName) ? "Player" : PlayerName.Trim();
        GameOverTitle = Lives > 0 ? $"Time's up, {name}!" : $"Out of lives, {name}!";

        scores.Add((name, Score));
        var latest = scores.Count - 1;
        BestScores.Clear();
        var rank = 1;
        foreach (var (index, entry) in scores.Index().OrderByDescending(s => s.Item.Score).ThenBy(s => s.Index).Take(BestScoreCount))
            BestScores.Add(new ScoreEntry(rank++, entry.Name, entry.Score, index == latest));

        State = GameState.GameOver;
    }

    private void StartRound()
    {
        Score = 0;
        Lives = MaxLives;
        TimeLeft = RoundLength;
        RoundStarted?.Invoke();
        State = GameState.Playing;
    }
}
