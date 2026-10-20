# Marketplace申請準備

作者 **y2sys**／無料公開／製品名 **Teams UIA Controls**。ここにある資料は申請準備用です。Maker Consoleへのアップロード・規約同意・審査提出・公開は行っていません。承認を保証するものではありません。

## 準備コマンド

```powershell
npm run prepare:marketplace
```

Node.js 24と.NET 10 SDKが必要です。出力先は `dist/marketplace/`。そこにある `.streamDeckPlugin` をアップロードしてください。PNGは別途メディア欄にアップロードする資料であり、プラグイン本体の代わりにはなりません。

- `com.y2sys.teams-uia-controls.streamDeckPlugin`：申請用プラグイン。
- `app-icon.png`：288×288。
- `thumbnail.png`：1920×960。
- `gallery-01-states.png`、`gallery-02-sharing.png`、`gallery-03-setup.png`：各1920×960。
- `listing.en.md`：英語の掲載内容、リリースノート、追加リンク。
- `reviewer-notes.en.md`：審査員向けの動作説明・検証状況・AI使用申告。
- `SHA256SUMS.txt`：配布候補ファイルのハッシュ。

画像はプラグイン自身のアイコンから作った説明図です。実機スクリーンショットや動作確認の証拠として扱わないでください。Microsoft/Elgatoのロゴ・他社のデバイス写真は使用していません。

## 公開用と開発用の識別子

申請版は `SDKVersion: 3` を宣言します。DRM保護はMaker Consoleで処理された後に適用されます。アップロード後に保護済みパッケージをダウンロードし、補助プロセスの起動と4操作を再確認してください。ローカルのパッケージ検証だけではDRM適用後の動作確認にはなりません。[公式DRM手順](https://docs.elgato.com/streamdeck/sdk/introduction/distribution/)

公開候補は `com.y2sys.teams-uia-controls`、従来の開発版は `com.meetingcontrols.streamdeck` です。公開要件に合わせ作者と製品名を含む識別子を用意し、既存のキー配置は変更しません。公開版は別プラグインになるためキーを配置し直す必要があります。同じ操作を両方から重複送信しないでください。公開後は識別子を固定します。

## 提出前に本人が確認すること

1. [Maker Console](https://maker.elgato.com/)で作者組織を作り、組織名が `y2sys` と一致することを確認。契約内容は本人が確認・同意します。
2. 希望する製品名がMarketplaceで空いているか確認。無料を選択します。作成後の名称・価格形態変更には制約があります。
3. GitHubのREADME・セットアップ・プライバシー・Issuesリンクが、ログアウト状態で閲覧できることと、公開してよい内容であることを確認。
4. 別PCまたはクリーンなWindows 11 x64で、.NET 10 Desktop Runtimeを先に導入し、**申請用パッケージ**をインストールして4操作を確認。仕事用アカウントと英語Teamsは未検証なので、確認するまでは対応済みと記載しません。
5. 審査用の動作動画を用意することを推奨します。会議名・参加者・会社情報を映さず、ミュート/解除、カメラ、共有先選択/停止、退出、アイコン連動を撮影します。動画は未作成です。提出画面や審査で動画を求められた場合は添付してください。
6. AI使用申告を確認。実装・資料・説明画像の作成にAIを使用しています。人間による要件決定・実機試験は行われていますが、現行ガイドラインではAIだけで作成された製品は対象外で、適格性はElgato審査次第です。開発経緯を隠したり、未実施の人手レビューを実施済みと書いたりしないでください。

## ホスティング不要の公開リンク

専用Webサイト、レンタルサーバー、独自ドメイン、GitHub Pagesは不要です。GitHubの通常の公開リポジトリで用意できます。

| 用途 | GitHubで用意するもの |
|---|---|
| 製品の説明 | 公開リポジトリのREADME |
| セットアップ | `docs/SETUP.en.md` の公開ページ |
| サポート | 公開リポジトリのIssuesを有効にする |
| プライバシー | `docs/PRIVACY.md` の公開ページ |

公開先は [y2sys/Teams-UIA-Controls](https://github.com/y2sys/Teams-UIA-Controls) です。サポートには、このリポジトリの [Issues](https://github.com/y2sys/Teams-UIA-Controls/issues) を使います。

`product.json` の `website` / `support` と掲載資料のリンクを公開先に揃えています。`publicLinksVerified` は製品ホームページの確認結果を表し、個々の文書の公開完了を保証するものではありません。準備コマンドは文書のpushや審査提出を実行しません。

## 提出手順

Maker Console → Create product → Stream Deck plugin → 申請用ファイルをアップロード → 英語の説明・無料設定・リンク → アイコン/サムネイル/ギャラリー3枚 → リリースノート・AI申告と必要な動作動画 → 内容を最終確認 → Submit for review。

審査後に自動公開したくない場合は、提出画面の **Automatically publish after being approved** をオフにします。現在は申請前です。

公開リンクの確認結果（2026-10-21）：リポジトリとIssuesは認証なしでHTTP 200を確認しました。`publicLinksVerified` は `true` とし、パッケージのURL欄に公開リポジトリを含めます。セットアップとプライバシー文書も本リポジトリに含めています。提出前に、掲載文にある個々のリンクもログアウト状態で確認してください。

## 公式資料（2026-10-21確認）

- [申請手順](https://docs.elgato.com/maker-console/submitting-products/)
- [製品・画像・AI使用のガイドライン](https://docs.elgato.com/guidelines/products/)
- [プラグインの作者・識別子・一覧アイコンのガイドライン](https://docs.elgato.com/guidelines/stream-deck/plugins/)
- [審査と公開設定](https://docs.elgato.com/maker-console/review-process/)

メディアの実寸・英語掲載文・一覧アイコンを要件に合わせています。英語のストア説明を準備したことは、英語Teamsの実機検証を完了したという意味ではありません。
