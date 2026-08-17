![GitHub release (latest by date)](https://img.shields.io/github/v/release/AmariNoa/Materilune?label=release)
![GitHub release (by tag)](https://img.shields.io/github/downloads/AmariNoa/Materilune/latest/total)
![GitHub all releases](https://img.shields.io/github/downloads/AmariNoa/Materilune/total?label=total%20downloads)
![GitHub issues](https://img.shields.io/github/issues/AmariNoa/Materilune)
![GitHub stars](https://img.shields.io/github/stars/AmariNoa/Materilune)

# Materilune

本パッケージは、VRChatアバターのマテリアル差し替えを、Modular AvatarのMaterial SwapとNDMFを土台に扱うUnityパッケージです。

## はじめに

アバターの各メッシュが使っているマテリアルを一覧し、差し替え先を選ぶだけで、Modular AvatarのMaterial Swapへ設定を反映します。差し替えの組み合わせをプリセットとして複数持つことができます。

元のマテリアルやメッシュを直接書き換えることはありません。設定はすべてアバター配下のオブジェクトとして保持され、非破壊的な改変ワークフローの中で扱えます。

## 導入

本パッケージはVPM (VRChat Package Manager) に対応しています。

### VCCへリポジトリを追加する

下記のリンクからVCCへリポジトリを追加できます。

[Add to VCC](vcc://vpm/addRepo?url=https%3A%2F%2Famarinoa.github.io%2FAmariNoa-VPM-Listing%2Findex.json)

ブラウザが許可を求めた場合は、VCCを開くことを許可してください。

### パッケージ一覧を表示する

パッケージ一覧を直接確認する場合は下記を開いてください。

[AmariNoa VPM Listing](https://amarinoa.github.io/AmariNoa-VPM-Listing/)

## 依存パッケージ

- Modular Avatar 1.13.0 以降
- NDMF 1.14.0 以降
- Unity Editor Localization Core 1.0.0 以降

## 使い方

1. Hierarchyで対象のアバターまたはオブジェクトを右クリックし、`Materilune > Setup Materilune` を実行します（GameObject メニューからも実行できます）。
2. セットアップ済みのオブジェクトには、Hierarchyの行に `Mt` ボタンが表示されます。これを押すとMateriluneウィンドウが開きます。
3. ウィンドウでプリセットを追加し、各メッシュのマテリアルに差し替え先を指定します。差し替え先を指定した設定のみがModular Avatarへ反映されます。

## 現在の状態

本パッケージはベータ版です。仕様と公開APIは変更される場合があります。

## パッケージ開発における AI の取り扱い

- 本パッケージの開発にはCodexおよびClaude Codeを使用しています。

---

# Materilune (EN README)

This package is a Unity package for swapping materials on VRChat avatars, built on Modular Avatar's Material Swap and NDMF.

## Introduction

It lists the materials used by each mesh under an avatar and, once you pick a replacement, writes the settings into Modular Avatar's Material Swap. Sets of replacements can be kept as presets.

The original materials and meshes are never edited. Everything is stored as objects under the avatar, so it fits into a non-destructive modification workflow.

## Installation

This package supports VPM (VRChat Package Manager).

### Add the repository to VCC

Use the link below to add the repository to VCC.

[Add to VCC](vcc://vpm/addRepo?url=https%3A%2F%2Famarinoa.github.io%2FAmariNoa-VPM-Listing%2Findex.json)

If your browser asks for permission, allow it to open VCC.

### View the package listing

To browse the listing directly, visit the page below.

[AmariNoa VPM Listing](https://amarinoa.github.io/AmariNoa-VPM-Listing/)

## Dependencies

- Modular Avatar 1.13.0 or later
- NDMF 1.14.0 or later
- Unity Editor Localization Core 1.0.0 or later

## Usage

1. Right-click the target avatar or object in the Hierarchy and run `Materilune > Setup Materilune` (also available from the GameObject menu).
2. A `Mt` button appears on the Hierarchy row of every object that has been set up. Press it to open the Materilune window.
3. Add a preset in the window and choose a replacement for each mesh material. Only entries with a replacement are written to Modular Avatar.

## Current state

This package is in beta. The specification and the public API may change.

## Use of AI in Package Development

- Codex and Claude Code are used in the development of this package.
