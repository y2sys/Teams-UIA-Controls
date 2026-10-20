# Teams UIA Controls

Windows UI Automationを使って、デスクトップ版Microsoft TeamsをStream Deckから制御するWindows向けプラグインです。マイク・カメラ・画面共有・退出の4ボタンと、Teamsの実状態に連動したアイコンを提供します。Zoom・Discordなど他の会議アプリは対象にしません。

Microsoft・Elgatoの公式プラグインではありません。TeamsのOAuth認証やアプリ登録は不要です。

## 別のPCで使う

### 必須ランタイム

**利用する各PCに [.NET 10 Desktop Runtime（Windows x64）](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) のインストールが必要です。ランタイムはプラグインに同梱していません。** ダウンロードページの「.NET Desktop Runtime」から「Windows x64」を選んでください。通常の.NET Runtimeだけでは中央表示に必要なWPFが含まれません。SDKやASP.NET Core Runtimeは不要です。

### インストール

1. 上記の.NET 10 Desktop Runtime（Windows x64）をインストールします。
2. Stream Deckソフトウェアとデスクトップ版Teamsをインストールします。
3. 配布ファイル `com.meetingcontrols.streamdeck.streamDeckPlugin` を開きます。
4. 「Teams UIA Controls」の4アクションをキーへ配置します。開発モードは不要です。

既存のボタン配置・設定を引き継ぐため、旧名「Meeting Controls」で使用していたプラグイン識別子・アクション識別子・配布ファイル名は維持しています。

- 対応CPU：Windows x64。初期検証はWindows 11を想定しています。ARM64版は未提供。
- Stream Deckソフトウェア：7.1以降。
- デスクトップ版Teams：個人用で初期検証済み。仕事用・組織の制限下では追加検証が必要です。
- Node.jsはStream Deckが管理します。
- Windows補助アプリはインストール済みの.NET 10 Desktop Runtimeを使用します。実行環境は同梱しません。PowerShell、.NET SDK、FlaUIの追加導入は不要です。

Windows標準の.NET Framework 4.xは.NET 10の代わりにはなりません。Desktop Runtimeが未導入の場合、補助アプリは起動できません。管理PCではランタイムの導入と通常の実行ファイルの実行許可が必要です。ランタイムの更新は配布先PCで管理してください。

## 操作と表示

- マイク：ミュート／解除。実際の状態を読み取ってアイコンへ反映。
- カメラ：オン／オフ。
- 共有：未共有ならTeamsの共有先選択画面を開く。共有中なら停止。
- 退出：通常押下で自分が退出。「全員の会議を終了」は選ばない。

通話判定は会議の退出ボタンを基準にします。検出済みの会議で共有先選択画面が開いた間は通話中を維持し、操作を一時的に無効化します。表示は共有ボタンだけが黄色になり、ほかのボタンは直前に確定した表示を保ちます。

複数の会議がある場合は自動選択せず、ボタンの設定画面で対象を指定します。この指定は4ボタンで共通です。指定会議が終了したら、自動選択へ戻してください。取得情報が古い場合や状態不明の場合は操作しません。

操作名や「通話なし」の文字はキーに表示せず、アイコンと色で状態を示します。通話中は下部に「Teams」のみを表示します。

マイク・カメラ操作でTeamsが前面に出た場合は、直前の作業ウィンドウへ戻します。共有・退出には適用しません。実際の入力や第三のアプリへの切り替え、元ウィンドウの終了・最小化を検知したら復帰を中止します。一瞬の前面化や、Windowsの制限で復帰できない場合はあります。入力通知では合成入力のフラグだけを判別し、キーの値・文字・座標は記録しません。

マイク・カメラの操作後にTeamsの実状態変化を確認すると、画面中央にアイコンと状態を約1.1秒表示します。表示はフォーカスを奪わず、クリックを透過します。初期状態、状態不明、操作未反映、共有・退出では表示しません。

## Marketplaceへの公開準備

無料公開・作者名 `yazirupi` の申請候補を `npm run prepare:marketplace` で生成できます。[申請準備手順](marketplace/README.md)、[英語セットアップ](docs/SETUP.en.md)、[プライバシー](docs/PRIVACY.md)を参照してください。公開用は作者名を含む別識別子を使い、既存の開発版のキー配置を維持します。申請・公開は自動では行いません。

## 開発環境

開発時にはNode.js 24以上と.NET 10 SDKが必要です。`global.json` で.NET 10のSDKを選択します。プロジェクト内の `.tools/dotnet/dotnet.exe` があればそれを優先し、それ以外はPATH上の `dotnet` を使います。`DOTNET_EXE` でSDKの場所を指定することもできます。

```powershell
npm ci
npm run typecheck
npm test
npm run stop
npm run build
npm run validate
npm run restart
npm run pack
```

初めて開発環境へ登録するときは `npm run link` を実行します。既存のボタン配置は自動変更しません。実行中の補助アプリを上書きできない場合があるため、再ビルド前には対象プラグインを停止してください。

`npm run build` はC#補助アプリをフレームワーク依存の単一EXEとして発行し、Node.js側をバンドルします。最終的な `bin/` に含めるのは `plugin.js` と `MeetingControls.Helper.exe` だけです。ランタイムの同梱、PowerShellスクリプト、C#ソースの実行時コンパイルはありません。

SDKのユーザーデータ、NuGetキャッシュ、発行用の中間ファイルは `.tools/` に置きます。SDK本体は配布パッケージに含めません。

## 構成

```text
Stream Deck → TypeScript / Node.js
                ↕ 標準入出力のJSON行
             C#補助アプリ（インストール済みの.NET 10 Desktop Runtimeを使用）
                ├ UI Automation：Teamsの検出・状態取得・操作
                ├ フォーカス復帰
                └ WPF：画面中央の状態表示
```

- `src/plugin.ts`：Teamsを操作するStream Deckの4アクション。
- `src/core.mjs`：Teams会議の選択とアイコン表示。
- `src/bridge.mjs`：C#実行ファイルの起動、JSON行通信、タイムアウト、再起動。
- `native/MeetingControls.Helper/`：C#の通信ループとTeams制御。
- `src/FocusReturnGuard.cs`、`src/TransitionNotices.cs`、`src/MeetingOsd.cs`：C#補助アプリへコンパイルする機能。
- `src/settings.html`：4ボタン共通の操作対象選択。

## 検証

- `npm test`：共通JavaScriptロジックとC#の動作条件テスト。
- `npm run test:smoke`：発行済み補助アプリの状態取得・無効対象の拒否。
- `npm run test:runtime`：子プロセスの.NET探索先を使えなくし、ランタイムがないと起動できないことを確認。PC全体の設定は変更しません。
- `node scripts/test-helper.mjs --osd-preview .diagnostics/osd-native-preview.png`：中央表示、フォーカス維持、クリック透過、自動消去を実機確認。
- `scripts/latency.mjs`：存在しない会議への要求に対する応答時間。実際のキー押下からTeams操作完了までの測定ではありません。

移植前に個人用Teams `26260.1717.5229.7049` でマイク・カメラ・共有の状態取得、最小化中の取得、ミュート切り替え、退出を確認しました。C#移植後の自動検証と、別PC・仕事用アカウントでの実機検証は区別してください。

UI AutomationのボタンID・状態ラベルはTeamsの公開APIではありません。日本語と主要な英語ラベルに対応し、未認識の状態では操作を無効にします。アクセシビリティ情報が取得できない場合にシステム設定を勝手に変更する処理はありません。過去のフォーカス調査は `docs/focus-investigation.md` に残しています。
