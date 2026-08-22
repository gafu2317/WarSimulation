# Unityロード時間改善の検証記録

## 基準

- 改善前のEditor Play開始: 約22秒
- 改善前の戦闘開始: 約34秒
- 条件: `Assets/Scenes/GafuTest.unity`、Unity 6000.4.3f1、同一Mac Editor

## 改善後

- 手動確認では、Domain Reload無効状態のPlay操作から編成画面とマップが操作可能になるまで6.6秒以内だった。
- 手動確認では、戦闘開始操作から戦闘HUDとキャラクター移動が確認できるまで3.7秒以内だった。この計測には確認用の2.5秒待機を含む。
- PlayModeテストの`GafuTest`ロードと別マップへの切替は722.2msだった。
- 同テスト区間のmain-thread allocation差分は0 bytesだった。
- 同テスト区間の`CombatLoading.Render3D`呼び出し回数は0だった。
- 同テスト区間の`CombatLoading.NavMeshBuild`呼び出し回数は0だった。
- 現在のStandalone対象のPlayerビルドは19.846秒で成功した。
- ビルド済みmacOS Playerで初回マップ準備、編成表示、戦闘開始まで確認した。
- Playerで戦闘開始操作から戦闘HUDとマップ表示を確認するまで3.855秒だった。この計測には確認用の3秒待機を含む。

## 再現手順

1. Unity 6000.4.3f1で`Assets/Scenes/GafuTest.unity`を開く。
2. Test RunnerのEditModeを全件実行し、全452件が成功することを確認する。
3. Test RunnerのPlayModeを全件実行し、プロジェクト側の全6件が成功することを確認する。
4. `CombatMapLoadingPlayModeTests.PrepareMapAsync_SwitchesAdditiveBakedScenesWithoutKeepingTheOldScene`の出力で時間とallocation差分を確認する。
5. Profilerで`CombatLoading.*`を表示し、Play開始、戦闘開始、同一マップ再戦、別マップ切替を操作する。
6. 正常系の操作中に`Render3D`、`NavMeshBuild`、全セル初期配置探索が存在しないことを確認する。
7. 現在のBuild ProfileでPlayerをビルドし、初回起動、編成表示、戦闘開始を確認する。

## ベイク更新

AuthoredMapを変更した場合は、Unityメニューの`WarSim/Map/全ランタイム用マップSceneを再ベイク`を実行する。fingerprintが一致しない場合、通常プレイは重い実行時生成へフォールバックせず、再ベイク要求を表示する。
