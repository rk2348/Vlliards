using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vlliards.App
{
    public sealed class GameShell : MonoBehaviour
    {
        private IGameSession session;
        private GameShellUI ui;
        private GameProfile profile;
        private Coroutine countdown;
        private bool sessionLive, practice, resultSaved, pausedCountdown;
        private string matchId;
        private float countdownRemaining;
        private GameScreen returnScreen;
        private Action confirmation;
        public GameScreen Screen { get; private set; }
        public bool IsPlaying => Screen == GameScreen.Playing;
        public GamePreferences Preferences => profile.Preferences;
        public GamePreferences OptionsDraft { get; private set; }
        public GameRecords Records => profile.Records;
        public MatchResult LastResult { get; private set; }
        public CpuDifficulty SelectedDifficulty { get; set; }
        public int OptionsTab { get; set; }
        public int HelpPage { get; private set; }
        public GameAudio Audio { get; private set; }
        public GameSnapshot Snapshot => session.CaptureSnapshot();
        public GameShellUI View => ui;

        public void Initialize(IGameSession game, TMP_FontAsset font, Camera camera, IProfileStorage storage = null)
        {
            session = game;
            profile = new GameProfile(storage ?? new PlayerProfileStorage());
            SelectedDifficulty = Preferences.difficulty;
            Audio = gameObject.AddComponent<GameAudio>();
            Audio.Initialize();
            ApplyPreferences(Preferences);
            ui = gameObject.AddComponent<GameShellUI>();
            ui.Initialize(this, font, camera);
            ShowTitleNow();
        }

        private void Update()
        {
            if (session == null) return;
            if (!Application.isBatchMode && Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame) Back();
                if (IsPlaying && Keyboard.current.rKey.wasPressedThisFrame) ConfirmRestart();
            }
            if (IsPlaying && Snapshot.finished) CompleteResult();
            ui.Tick(Snapshot, Preferences);
        }

        private void Display(GameScreen screen)
        {
            Screen = screen;
            ui.Show(screen);
        }

        public void ShowMatchSetup() { SelectedDifficulty = Preferences.difficulty; Display(GameScreen.MatchSetup); }

        public void StartMatch(bool training)
        {
            if (countdown != null) StopCoroutine(countdown);
            practice = training;
            sessionLive = true;
            resultSaved = false;
            pausedCountdown = false;
            matchId = Guid.NewGuid().ToString("N");
            var preferences = Preferences.Clone();
            preferences.difficulty = SelectedDifficulty;
            profile.SavePreferences(preferences);
            session.BeginMatch(training, SelectedDifficulty);
            session.SetPaused(true);
            countdownRemaining = 3;
            Display(GameScreen.Countdown);
            countdown = StartCoroutine(Countdown());
        }

        private IEnumerator Countdown()
        {
            while (countdownRemaining > 0)
            {
                if (Screen == GameScreen.Countdown)
                {
                    ui.SetCountdown(Mathf.CeilToInt(countdownRemaining).ToString());
                    countdownRemaining -= Time.unscaledDeltaTime;
                }
                yield return null;
            }
            countdown = null;
            if (!sessionLive) yield break;
            session.SetPaused(false);
            Display(GameScreen.Playing);
            ui.FlashGo();
        }

        public void Pause()
        {
            if (Screen != GameScreen.Playing && Screen != GameScreen.Countdown) return;
            pausedCountdown = Screen == GameScreen.Countdown;
            session.SetPaused(true);
            Display(GameScreen.Paused);
        }

        public void Resume()
        {
            if (Screen != GameScreen.Paused) return;
            if (pausedCountdown) Display(GameScreen.Countdown);
            else { session.SetPaused(false); Display(GameScreen.Playing); }
        }

        public void RequestTitle()
        {
            if (sessionLive && !Snapshot.finished)
                Confirm("タイトルに戻りますか？", "進行中の対戦を終了します。この対戦は戦績に記録されません。", ShowTitleNow);
            else ShowTitleNow();
        }

        public void ShowTitleNow()
        {
            if (countdown != null) { StopCoroutine(countdown); countdown = null; }
            sessionLive = false;
            session.ReturnToTitle();
            Display(GameScreen.Title);
        }

        public void ConfirmRestart() => Confirm("最初からやり直しますか？", "現在の盤面をリセットします。オプションと過去の戦績は保持されます。", () => StartMatch(practice));
        public void Rematch() => StartMatch(practice);

        private void CompleteResult()
        {
            if (resultSaved) return;
            var state = Snapshot;
            LastResult = new MatchResult
            {
                id = matchId, won = state.playerWon, practice = practice, difficulty = SelectedDifficulty,
                seconds = state.elapsed, shots = state.shots, fouls = state.fouls, pockets = state.pocketed
            };
            profile.CommitResult(LastResult);
            resultSaved = true;
            Audio.Result(LastResult.won);
            Display(GameScreen.Results);
        }

        public void OpenOptions()
        {
            if (IsPlaying) Pause();
            returnScreen = Screen;
            OptionsDraft = Preferences.Clone();
            OptionsTab = 0;
            Display(GameScreen.Options);
        }

        public void PreviewOptions() { OptionsDraft.Validate(); ApplyPreferences(OptionsDraft); }
        public void ApplyOptions()
        {
            profile.SavePreferences(OptionsDraft);
            ApplyPreferences(Preferences);
            Display(returnScreen);
        }
        public void CancelOptions() { ApplyPreferences(Preferences); Display(returnScreen); }
        public void DefaultOptions()
        {
            Confirm("設定を初期値に戻しますか？", "戦績は削除されません。「適用」で設定を保存します。", () =>
            {
                OptionsDraft = new GamePreferences { difficulty = Preferences.difficulty, hasReadHelp = Preferences.hasReadHelp };
                PreviewOptions();
                Display(GameScreen.Options);
            });
        }
        private void ApplyPreferences(GamePreferences settings)
        {
            session.ApplyPreferences(settings);
            Audio?.SetVolume(settings.effectsVolume);
        }

        public void OpenHelp()
        {
            if (IsPlaying) Pause();
            returnScreen = Screen;
            HelpPage = 0;
            Display(GameScreen.Help);
        }
        public void NextHelp()
        {
            if (HelpPage < 2) { HelpPage++; Display(GameScreen.Help); return; }
            var preferences = Preferences.Clone(); preferences.hasReadHelp = true; profile.SavePreferences(preferences);
            Display(returnScreen);
        }
        public void PreviousHelp() { if (HelpPage > 0) { HelpPage--; Display(GameScreen.Help); } else Display(returnScreen); }
        public void ShowRecords() => Display(GameScreen.Records);
        public void ShowCredits() => Display(GameScreen.Credits);
        public void ResetRecords() => Confirm("戦績をリセットしますか？", "保存された対戦記録と自己ベストを削除します。この操作は取り消せません。", () => { profile.ResetRecords(); Display(GameScreen.Records); });

        public string ConfirmTitle { get; private set; }
        public string ConfirmBody { get; private set; }
        private GameScreen confirmReturn;
        private void Confirm(string title, string body, Action action)
        {
            if (IsPlaying) Pause();
            confirmReturn = Screen;
            ConfirmTitle = title; ConfirmBody = body; confirmation = action;
            Display(GameScreen.Confirm);
        }
        public void AcceptConfirmation() { var action = confirmation; confirmation = null; action?.Invoke(); }
        public void CancelConfirmation() { confirmation = null; Display(confirmReturn); }
        public void RequestQuit() => Confirm("ゲームを終了しますか？", "保存済みの設定と戦績は次回も引き継がれます。", () =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });

        public void Back()
        {
            switch (Screen)
            {
                case GameScreen.Playing: case GameScreen.Countdown: Pause(); break;
                case GameScreen.Paused: Resume(); break;
                case GameScreen.Options: CancelOptions(); break;
                case GameScreen.Confirm: CancelConfirmation(); break;
                case GameScreen.Help: Display(returnScreen); break;
                case GameScreen.MatchSetup: case GameScreen.Records: case GameScreen.Credits: ShowTitleNow(); break;
            }
        }
    }
}
