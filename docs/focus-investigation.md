# Teamsのミュート操作に伴うフォーカス移動の切り分け

この文書はPowerShell版で実施した調査の記録。以下に記載する診断用 `.ps1` は、C#実行ファイルへの移行に伴い削除した。現在の実装は `native/MeetingControls.Helper/` にあり、フォーカス復帰・状態通知などの回帰チェックは `npm run test:helper` で実行する。移行によって操作経路そのものを変更したわけではなく、UI Automationと既存の緩和策を維持している。

## 結論

2026-10-20、個人用Teams 26260.1717.5229.7049のテスト会議で、Stream Deckプラグインを停止した状態でも、UI Automationの `InvokePattern.Invoke()` だけでマイクを操作すると、Teamsが前面へ出ることを確認した。

これは今回のTeams環境と呼び出し方式での再現結果であり、すべてのアプリでUI Automationを使うと必ずフォーカスが移る、という結論ではない。Teams/WebView側のどの内部処理が前面化を起こすかまでは特定していない。

## 手順と結果

1. 現行コードに `SetFocus`、`SetForegroundWindow`、ウィンドウの復元、マウス・キー入力の処理がないことを確認。
2. 公式CLIでMeeting Controlsだけを停止。
3. `scripts/focus-probe.ps1` から、指定したTeams会議ウィンドウのマイクボタンを取得。
4. 読み取り前後と、300msの待機後でも、別アプリが前面のままであることを確認。
5. UI AutomationのInvokeを1回実行。
6. Invoke後の80ms・330msの観測で、Teams会議ウィンドウが前面へ移ったことを確認。
7. Meeting Controlsを再起動。

テストではミュート解除中からミュートへ切り替わった。フォーカスを戻す処理や、ウィンドウ状態を変える回避策は実装していない。

## 再テスト

同日、ユーザーの依頼で同じ最小テストを再実施。Teams会議ウィンドウは通常表示で最小化されていないことを確認した。読み取り前・読み取り後・操作直前の3点すべてで、同じTeams以外のウィンドウが前面だった。ミュート解除のInvokeが戻った直後にはTeams会議が前面になり、80ms後・330ms後もTeamsのままだった。2回の最小テストで同じ方向のフォーカス移動を確認した。再テスト後のマイクはミュート解除状態。

## 観測の限界

- ユーザーからは「最小化中は前面に出ないが、通常ウィンドウが非アクティブだと前面に出る」と報告されている。今回の最小再現は後者を対象とした。
- UI Automationの全方式・別のTeamsバージョン・仕事用アカウントとの比較は未実施。
- 検証スクリプトは既定で読み取りだけを行い、`-Invoke` を明示した場合にだけミュートを1回切り替える。元の前面ウィンドウがTeamsだったり、読み取り中に前面ウィンドウが変わった場合は操作しない。

## UIA3との比較

本体は変更せず、`scripts/focus-probe-uia3.ps1` でネイティブCOMの `CUIAutomation8` と `IUIAutomationInvokePattern.Invoke()` を比較した。FlaUI.UIA3の初期化・呼び出しに対応する経路で、FlaUI全体を導入するものではない。

比較時もMeeting Controlsを停止。Vivaldiが前面の状態を読み取り前・読み取り後・操作直前に確認した。UIA3経由でミュートを実行すると、Invokeが戻った直後からTeams会議が前面になり、80ms後・330ms後も同じだった。操作はミュート解除中からミュートへの切り替え。現行.NET経由からUIA3へ変更するだけでは、この環境での前面化は解消しなかった。

MuteDeckの公開更新履歴v4.11にはWindowsでミュート・カメラを背後のまま操作できることが記載されている。ただし今回確認した公開資料には、その具体的なWindows API呼び出しやコードの記載は見つからなかった。MuteDeckがUIA3を使っているとは確認していない。

比較スクリプトの依存はNuGetの `Interop.UIAutomationClient 10.19041.0` のnet45アセンブリ。`.diagnostics/uia3/interop/` に展開した診断専用ファイルを使用する。本体に依存を追加したり、操作方式を変更したりしていない。

## 後続の緩和策

上記切り分けの後、ユーザーの明示的な実装依頼により、ミュート・カメラ限定のフォーカス復帰を追加した。共有・退出の操作方式は変更していない。`src/FocusReturnGuard.cs` が元の作業ウィンドウのハンドルと所有プロセスを保持し、Teamsへ移った場合だけ短い時間内に復帰を試みる。

初期案のGetLastInputInfoによる判定では、UI Automationの操作自体に伴う入力時刻更新で中止された。物理入力の発生だけをWindowsの低レベル通知で区別した再テストでは、物理入力0件・合成入力2件を観測した。Vivaldiが前面の状態からミュート解除を実行し、直後にはTeamsが前面となり、復帰処理後・80ms後・330ms後は元のVivaldiが前面だった。入力内容やキーコードは記録していない。

この結果は一瞬の前面化を完全に防いだ証明ではない。Windowsの前面化制限によって復帰できないケースも考慮し、無限再試行や入力注入による制限回避は行わない。

参考資料:
- https://mutedeck.com/changelog/ （v4.11の背景操作対応、v4.12のmacOS限定の挙動変更を区別）
- https://mutedeck.com/help/features/microsoft-teams-setup/
- https://github.com/FuSan21/Macro-Deck-Teams-Plugin/blob/main/Uia/UiaControlSurface.cs
- https://github.com/FlaUI/FlaUI/blob/main/src/FlaUI.UIA3/UIA3Automation.cs
