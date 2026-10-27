using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vlliards.App
{
    public sealed partial class GameShellUI
    {
        private void BuildTitle()
        {
            var column = Stack(menu, "Title actions", 0, 10);
            Place(column, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(76, 0), new Vector2(580, 748));
            Text(column, "REAL-TIME BILLIARDS", 16, Gold, 28).characterSpacing = 5;
            var logo = Text(column, "VLLIARDS", 88, Color.white, 112); logo.fontStyle = FontStyles.Bold; logo.characterSpacing = 7;
            Text(column, "その一瞬を、狙い撃つ。", 30, Color.white, 48);
            Text(column, "手番なし。相手の構えを読み、9番を奪え。\n球の目線で遊ぶリアルタイム・ビリヤード。", 21, Muted, 70);
            Button(column, "CPUと対戦", shell.ShowMatchSetup, true, 68);
            Button(column, "ひとりで練習", () => shell.StartMatch(true), false, 54);
            var row = Row(column);
            Button(row, "あそび方", shell.OpenHelp); Button(row, "オプション", shell.OpenOptions);
            row = Row(column);
            Button(row, "戦績", shell.ShowRecords); Button(row, "クレジット", shell.ShowCredits);
            if (!Application.isMobilePlatform) Button(column, "終了", shell.RequestQuit, false, 44);
            Text(column, shell.Preferences.hasReadHelp ? "準備ができたら、テーブルへ。" : "はじめての方は「あそび方」から。", 17, Muted, 32);

            var stats = Image(menu, "Record summary", Panel);
            Place(stats.rectTransform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-76, -112), new Vector2(320, 230));
            var stack = Stack(stats.transform, "Summary", 24, 10); Stretch(stack);
            Text(stack, "YOUR RECORD", 15, Gold, 24).characterSpacing = 3;
            Text(stack, shell.Records.wins + " WIN", 45, Color.white, 60);
            Text(stack, "対戦 " + shell.Records.matches + " 回   /   勝率 " + WinRate(), 20, Muted, 35);
            Text(stack, "ベスト " + (shell.Records.bestWinSeconds > 0 ? FormatTime(shell.Records.bestWinSeconds) : "—"), 20, Muted, 35);
            var version = Text(menu, "VLLIARDS  /  " + Application.version, 14, Muted);
            Place(version.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 20), new Vector2(330, 28)); version.alignment = TextAlignmentOptions.Right;
        }

        private void BuildSetup()
        {
            var content = Card("対戦をはじめる", "相手の構えを読み、自分のタイミングで撃つ。", 860, 620);
            Text(content, "CPUの強さ", 23, Gold, 36);
            Choices(content, new[] { "やさしい", "ふつう", "むずかしい" }, (int)shell.SelectedDifficulty, index => shell.SelectedDifficulty = (CpuDifficulty)index);
            Text(content, "やさしい：長めに構える、狙いに余裕のある相手\nふつう：基本の駆け引きを楽しめる相手\nむずかしい：素早く正確に狙う相手", 22, Muted, 118);
            Text(content, "共通ルール：発射後3秒のクールタイム / ファウルは5秒停止\n最小番号に最初に当て、合法的に9番を入れると勝利。", 21, Color.white, 78);
            Button(content, "対戦スタート", () => shell.StartMatch(false), true, 60);
            var row = Row(content, 48); Button(row, "あそび方", shell.OpenHelp); Button(row, "戻る", shell.ShowTitleNow);
        }

        private void BuildCountdown()
        {
            var content = Card("READY", shell.Snapshot.practice ? "PRACTICE / ひとりで練習" : "VS CPU / " + DifficultyName(shell.SelectedDifficulty), 540, 470);
            countText = Text(content, "3", 140, Gold, 190); countText.alignment = TextAlignmentOptions.Center;
            Text(content, "マウスで狙い、長押しして離すと発射。", 20, Muted, 42).alignment = TextAlignmentOptions.Center;
            Button(content, "キャンセル", shell.RequestTitle, false, 46);
        }

        private void BuildPause()
        {
            var content = Card("一時停止", "テーブルの時間は止まっています。", 680, 650);
            Button(content, "ゲームに戻る", shell.Resume, true, 62);
            Button(content, "オプション", shell.OpenOptions);
            Button(content, "あそび方", shell.OpenHelp);
            Button(content, "最初からやり直す", shell.ConfirmRestart);
            Button(content, "タイトルに戻る", shell.RequestTitle);
            Text(content, "Escキーでもゲームに戻れます。", 18, Muted, 32);
        }

        private void BuildResults()
        {
            var result = shell.LastResult;
            bool won = result != null && result.won;
            var content = Card(won ? result.practice ? "PRACTICE CLEAR" : "YOU WIN" : "CPU WINS", won ? "9番を合法的にポケットしました。" : "相手が9番をポケットしました。次の一戦へ。", 900, 650);
            Text(content, won ? "勝利を、その手に。" : "次は、その一瞬を。", 32, won ? Gold : Muted, 58);
            var row = Row(content, 114);
            Stat(row, "TIME", FormatTime(result?.seconds ?? 0));
            Stat(row, "SHOTS", (result?.shots ?? 0).ToString());
            Stat(row, "FOULS", (result?.fouls ?? 0).ToString());
            Text(content, result != null && result.practice ? "練習のクリア記録を保存しました。" : "対戦結果を戦績に保存しました。", 20, Muted, 46);
            Button(content, result != null && result.practice ? "もう一度練習する" : "もう一度対戦する", shell.Rematch, true, 64);
            Button(content, "タイトルに戻る", shell.RequestTitle);
        }

        private void BuildOptions()
        {
            var content = Card("オプション", "変更を確認し、「適用」で保存します。", 900, 720);
            var tabs = Row(content, 48);
            string[] names = { "サウンド", "操作", "画面" };
            for (int i = 0; i < names.Length; i++)
            {
                int tab = i;
                Button(tabs, names[i], () => { shell.OptionsTab = tab; Show(GameScreen.Options); }, shell.OptionsTab == i, 48);
            }
            var settings = shell.OptionsDraft;
            if (shell.OptionsTab == 0)
            {
                OptionSlider(content, "マスター音量", settings.masterVolume, 0, 1, value => settings.masterVolume = value, Percent);
                OptionSlider(content, "BGM", settings.musicVolume, 0, 1, value => settings.musicVolume = value, Percent);
                OptionSlider(content, "効果音・操作音", settings.effectsVolume, 0, 1, value => settings.effectsVolume = value, Percent);
                Text(content, "音量は変更中もプレビューできます。", 18, Muted, 40);
            }
            else if (shell.OptionsTab == 1)
            {
                OptionSlider(content, "視点感度", settings.sensitivity, .03f, .3f, value => settings.sensitivity = value, value => value.ToString("0.00"));
                OptionSlider(content, "視野角", settings.fieldOfView, 65, 95, value => settings.fieldOfView = value, value => Mathf.RoundToInt(value) + "°");
                OptionToggle(content, "上下の視点操作を反転", settings.invertY, value => settings.invertY = value);
                OptionToggle(content, "照準・自分のガイドを表示", settings.showGuide, value => settings.showGuide = value);
            }
            else
            {
                Text(content, "画質", 23, Gold, 32);
                Choices(content, new[] { "軽量", "標準", "高画質" }, settings.graphicsQuality, value => { settings.graphicsQuality = value; shell.PreviewOptions(); });
                Text(content, "フレームレート上限", 23, Gold, 32);
                int[] rates = { 30, 60, 90, 120 };
                Choices(content, new[] { "30", "60", "90", "120" }, Array.IndexOf(rates, settings.frameRate), value => { settings.frameRate = rates[value]; shell.PreviewOptions(); });
                OptionToggle(content, "テーブルレーダーを表示", settings.showRadar, value => settings.showRadar = value);
                OptionToggle(content, "メニュー背景の動きを抑える", settings.reduceMotion, value => settings.reduceMotion = value);
            }
            var spacer = Node(content, "Spacer").gameObject.AddComponent<LayoutElement>(); spacer.flexibleHeight = 1;
            var footer = Row(content, 54);
            Button(footer, "初期値", shell.DefaultOptions);
            Button(footer, "キャンセル", shell.CancelOptions);
            Button(footer, "適用", shell.ApplyOptions, true);
        }

        private void OptionSlider(Transform parent, string label, float value, float min, float max, Action<float> change, Func<float, string> format)
        {
            var block = Stack(parent, label, 0, 6); Height(block, 80);
            var row = Row(block, 32);
            Text(row, label, 22, Color.white, 32);
            var number = Text(row, format(value), 21, Gold, 32); number.alignment = TextAlignmentOptions.Right;
            var sliderRect = Node(block, label + " slider"); Height(sliderRect, 30);
            var track = Image(sliderRect, "Track", new Color(.14f, .24f, .28f));
            track.rectTransform.anchorMin = new Vector2(0, .5f); track.rectTransform.anchorMax = new Vector2(1, .5f); track.rectTransform.sizeDelta = new Vector2(-18, 5); track.rectTransform.anchoredPosition = Vector2.zero;
            var fill = Image(track.transform, "Fill", Gold); Stretch(fill.rectTransform);
            var handleArea = Node(sliderRect, "Handle area"); Stretch(handleArea, 9);
            var handle = Image(handleArea, "Handle", Color.white);
            Place(handle.rectTransform, new Vector2(.5f, .5f), Vector2.one * .5f, Vector2.zero, new Vector2(18, 26));
            var slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.minValue = min; slider.maxValue = max; slider.value = value;
            slider.onValueChanged.AddListener(current => { change(current); number.text = format(current); shell.PreviewOptions(); });
        }

        private void OptionToggle(Transform parent, string label, bool value, Action<bool> change)
        {
            var background = Image(parent, label, new Color(.075f, .13f, .17f)); Height(background.transform, 50);
            var box = Image(background.transform, "Box", new Color(.16f, .25f, .29f));
            Place(box.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(12, 0), new Vector2(28, 28));
            var check = Image(box.transform, "Selected", Gold); Stretch(check.rectTransform, 5);
            var text = Text(background.transform, label, 21, Color.white); Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(54, 0); text.rectTransform.offsetMax = new Vector2(-10, 0); text.alignment = TextAlignmentOptions.MidlineLeft;
            var toggle = background.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = box; toggle.graphic = check; toggle.isOn = value;
            toggle.onValueChanged.AddListener(current => { change(current); shell.Audio.Click(); shell.PreviewOptions(); });
        }

        private void Choices(Transform parent, string[] labels, int selected, Action<int> select)
        {
            var row = Row(parent, 50);
            var buttons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                buttons[i] = Button(row, labels[i], () =>
                {
                    select(index);
                    for (int j = 0; j < buttons.Length; j++)
                    {
                        buttons[j].GetComponent<Image>().color = j == index ? Gold : new Color(.10f, .17f, .21f);
                        buttons[j].GetComponentInChildren<TMP_Text>().color = j == index ? Ink : Color.white;
                    }
                }, i == selected, 50);
            }
        }

        private void BuildHelp()
        {
            string[] titles = { "01 / 球の目線で動く", "02 / 9番を狙え", "03 / 相手の構えを読む" };
            string[] descriptions =
            {
                "WASD：見ている方向に移動\nマウス：視点を動かして照準\n左クリック長押し：キューを引いて溜める\nボタンを離す：狙った方向に発射\nSpace：一定の力で素早く発射\nEsc：一時停止・オプション",
                "手番はありません。発射後3秒で、また打てます。\n最初に当てるのは、台に残る最小番号の球。\nその後に入球、またはいずれかの球のクッション接触が必要です。\n空振り・番号違い・スクラッチは5秒停止。\n合法的に9番を入れた側が勝利します。\nファウルと同時に入った9番は台に戻ります。",
                "相手のキュー・手の動きとオレンジ色のリングが、発射の合図。\nCPUは構えてから打ち、最後の瞬間には狙いを変えません。\n相手が狙う球を先に動かすか、入球を狙うか。\n自分と相手のクールタイムを見て、タイミングを選びましょう。\n「ひとりで練習」ではCPUなしで入球を試せます。"
            };
            var content = Card("あそび方", titles[shell.HelpPage], 940, 700);
            Text(content, descriptions[shell.HelpPage], 23, Color.white, 320);
            Text(content, "感度・視野角・ガイドは「オプション」で調整できます。", 19, Muted, 50);
            var row = Row(content, 56);
            Button(row, "前へ", shell.PreviousHelp);
            Button(row, shell.HelpPage == 2 ? "閉じる" : "次へ", shell.NextHelp, true);
        }

        private void BuildRecords()
        {
            var content = Card("戦績", "この端末に保存された対戦と練習の記録です。", 940, 720);
            var row = Row(content, 94);
            Stat(row, "MATCHES", shell.Records.matches.ToString());
            Stat(row, "WINS", shell.Records.wins.ToString());
            Stat(row, "WIN RATE", WinRate());
            Text(content, "ベスト " + (shell.Records.bestWinSeconds > 0 ? FormatTime(shell.Records.bestWinSeconds) : "—") + "   /   練習クリア " + shell.Records.practiceClears + " 回", 21, Gold, 42);
            Text(content, "最近のプレイ", 21, Color.white, 32);
            if (shell.Records.recent.Count == 0) Text(content, "まだ記録はありません。最初の一戦を始めましょう。", 21, Muted, 110);
            else for (int i = 0; i < Mathf.Min(4, shell.Records.recent.Count); i++)
            {
                var result = shell.Records.recent[i];
                string date = DateTime.TryParse(result.playedAtUtc, out var parsed) ? parsed.ToLocalTime().ToString("MM/dd HH:mm") : "—";
                Text(content, date + "    " + (result.practice ? "練習" : result.won ? "WIN" : "LOSE") + "    " + FormatTime(result.seconds) + "    " + result.shots + " shots", 20, result.won ? Mint : Muted, 36);
            }
            var footer = Row(content, 52); Button(footer, "記録をリセット", shell.ResetRecords); Button(footer, "戻る", shell.ShowTitleNow, true);
        }

        private void BuildCredits()
        {
            var content = Card("クレジット", "VLLIARDS", 820, 580);
            Text(content, "GAME ASSETS", 18, Gold, 32);
            Text(content, "Billiard Pool Table\nBar Lounge", 26, Color.white, 90);
            Text(content, "TYPEFACE", 18, Gold, 32);
            Text(content, "Noto Sans CJK JP\nSIL Open Font License 1.1", 23, Muted, 86);
            Text(content, "遊んでいただき、ありがとうございます。", 22, Color.white, 48);
            Button(content, "タイトルに戻る", shell.ShowTitleNow, true);
        }

        private void BuildConfirm()
        {
            var content = Card(shell.ConfirmTitle, "", 760, 390);
            Text(content, shell.ConfirmBody, 23, Muted, 110);
            var row = Row(content, 58);
            Button(row, "キャンセル", shell.CancelConfirmation);
            Button(row, "実行する", shell.AcceptConfirmation, true);
        }

        private void Stat(Transform parent, string label, string value)
        {
            var background = Image(parent, label, new Color(.07f, .12f, .15f));
            var layout = background.gameObject.AddComponent<LayoutElement>(); layout.flexibleWidth = 1;
            var content = Stack(background.transform, label, 14, 4); Stretch(content);
            Text(content, label, 13, Muted, 20).characterSpacing = 2;
            Text(content, value, 32, Gold, 44);
        }
        private string WinRate() => shell.Records.matches > 0 ? Mathf.RoundToInt((float)shell.Records.wins / shell.Records.matches * 100) + "%" : "—";
        private static string Percent(float value) => Mathf.RoundToInt(value * 100) + "%";
        public static string DifficultyName(CpuDifficulty value) => value == CpuDifficulty.Easy ? "やさしい" : value == CpuDifficulty.Hard ? "むずかしい" : "ふつう";
    }
}
