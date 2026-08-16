# Changelog

すべての注目すべき変更をこのファイルに記録します。

形式は [Keep a Changelog](https://keepachangelog.com/ja/1.0.0/) に基づいており、
このプロジェクトは [Semantic Versioning](https://semver.org/spec/v2.0.0.html) に準拠しています。

## [Unreleased]

### Added

### Changed

### Deprecated

### Removed

### Fixed

### Security

## [0.1.0-beta.1] - 2026-08-16

最初の公開版です。

### Added

- セットアップ機能。対象オブジェクトの配下へMateriluneの管理階層を生成し、Modular AvatarのMaterial Swapを配置します。
- プリセット機構。差し替えの組み合わせを複数持ち、単一のプリセットのみを有効にできます。
- 置換エントリの自動生成。対象メッシュが使用しているマテリアルからエントリを生成します。置換元は編集できず、置換先を指定したエントリのみがModular Avatarへ反映されます。
- Materiluneウィンドウ。プリセット一覧、プリセット全体の差し替え、対象メッシュのツリー、選択中メッシュの差し替えを1画面で編集できます。
- マテリアル候補の選択ポップアップ。候補をモード別のタブに分けて表示し、プレビューを添えて選べます。
- Hierarchyの行に表示するウィンドウ起動ボタン。同じ行に並ぶ他ツールのボタンとは、セッション内で共有するレジストリを介して位置を調停します。
- 各コンポーネントのインスペクタに設定の要約とウィンドウを開くボタンを表示します。Materiluneが管理するRendererには警告を表示します。
- 日本語と英語の表示切り替え。
- `Preferences > AmariNoa > Hierarchy Buttons` に、Hierarchyボタンの追加余白の設定を追加しました。

### Known limitations

- 言語設定がUnityエディタ全体・全パッケージ共通である旨の注記表示は未実装です。
- Materiluneウィンドウの対象オブジェクトを固定するロックトグルは未実装です。
- VPMリスティングへの登録先URLは確定していません。
- Modular Avatar 1.13.x での公開APIの検証は未実施です。動作確認は1.17.1で行っています。
