# AI_BUG_REGISTRY

> 모드: **BUG-STABILIZATION-FIRST** (2026-07-06 전환)
> 원칙: 진단 → 근본 원인 → 최소 수정. 코스메틱 리팩토링 금지.

## 요약표

| ID | 증상 | 상태 | 우선순위 | 코드만으로 수정 | 에디터 확인 |
|---|---|---|---|---|---|
| BUG-1 | 사망/재시작 후 매니저·플레이어·카메라·UI만 있고 방/맵 없음 | **근본 원인 확인됨** | 1 | 부분 가능 (완전 수정은 씬/프리팹 데이터 보수 병행 권장) | 필요 (재현 확인 + 데이터 수정 승인) |
| BUG-2 | spr_4 보스전 중 위쪽 문이 안 막힘 | 조사 중 (구조 파악 완료) | 2 | 미정 | 필요 |
| BUG-3 | 필드 몬스터가 방 문을 무시하고 통과 | 원인 구조 확인됨 | 2 | 부분 가능 (레이어/콜라이더 정책 결정 필요) | 필요 |
| BUG-4 | 문 근처에서 찌르기 넉백 시 몬스터가 위로 튕겨 나감 | 미조사 | 3 | 미정 | 필요 |
| BUG-5 | 보스 투사체 콜라이더/판정 범위 불일치 | 미조사 | 4 | 미정 | 필요 (프리팹 콜라이더 실측) |
| BUG-6 | 필드 몬스터 이동이 이상함 | 미조사 | 5 | 미정 | 필요 |

---

## BUG-1: 사망/재시작 후 방/맵 미로드 — 근본 원인 확인됨

1. **증상**: 사망 → Restart 후 Camera, EventSystem, NewPlayer, MainCanvas, DontDestroyOnLoad(Sound/Inventory/Game/SaveManager), Debug Updater만 남고 방이 하나도 스폰되지 않음.
2. **재현 경로(가설)**: `allMapRooms`에 등록되지 않은 방(아래 목록)에서 저장하거나 사망 → Restart → `RoomManager.RefreshRoomState()`가 좌표 매칭 실패 → 방 0개 스폰.
3. **의심 파일/클래스**:
   - `Assets/Scripts/Field/RoomManager.cs` — `RefreshRoomState()`, `GetRoomDataByCoord()`
   - `Assets/Prefabs/Managers/GameManager.prefab` — `allMapRooms` 13개
   - `Assets/Scenes/FIeld.unity` — 인스턴스 오버라이드로 sum_2/sum_1/sum_3 추가(16개, sum_3 중복)
4. **코드 근거**:
   - 재시작 시 `RestoreRoutine()` → `RefreshRoomState()` → `CalculateGridCoord(player.position)` → `GetRoomDataByCoord()`는 **allMapRooms 목록만** 검색.
   - 최종 유효 목록(16개)에 **`aut_3`(1,6), `Ending`(1,4), `spr_1 BOSS`(0,1) 누락**.
   - `aut_3`은 `aut_2.east` 이웃으로 플레이 중 정상 진입 가능하지만, 그 안에서 사망/재시작하면 좌표 (1,6) 매칭 실패 → `DWarn("No RoomData matched...")` 후 방을 하나도 스폰하지 않고 종료. 관측된 증상과 정확히 일치.
   - 평상시 방 이동은 `RoomData` 이웃 참조로 동작하므로 allMapRooms 누락이 드러나지 않음 — 재시작 경로에서만 터짐.
   - 추가 데이터 결함: `spr_1`/`spr_1 BOSS` 좌표 중복 (0,1), `Ending`/`sum_1` 좌표 중복 (1,4). `spr_4` 프리팹 북쪽 문은 `Ending`으로 연결되지만 `spr_4.asset`의 north는 `sum_1` → 같은 자리 (1,4)에 두 방이 겹쳐 스폰될 수 있음 (`OVERLAP DETECTED` 경고 대상).
   - **진단 방해 요소**: 작업 트리의 미커밋 `FIeld.unity` 변경에 `debugLogs: 0`이 포함 — RoomManager 경고 로그가 꺼져 있어 콘솔 단서가 안 보임.
5. **코드만으로 수정 가능?**: 부분 가능. `RoomManager.Start()`에서 allMapRooms를 이웃 그래프(BFS)로 확장해 누락 방을 자동 포함하는 코드-온리 하드닝 가능(직렬화 필드 무변경). 단, 좌표 중복(Ending/sum_1)의 우선순위 문제와 spr_4 north 데이터 불일치는 데이터(에셋) 정리가 정공법.
6. **에디터 확인 필요?**: 필요 — (a) aut_3에서 사망 → 재시작 재현, (b) 콘솔에서 `No RoomData matched` 확인(단, debugLogs 켜야 함), (c) allMapRooms에 aut_3/Ending 추가는 씬 또는 프리팹 수정이므로 오너 승인 필수.
7. **권장 다음 행동**: 오너 결정 카드 참조 (AI_DECISION_LOG #2026-07-06-1).
8. **위험도**: 코드-온리 BFS 확장 = 낮음. 씬/프리팹 데이터 수정 = 중간(에디터에서 수행해야 안전).
9. **Codex 실행 가능 태스크**: `RoomManager.Start()`에 "allMapRooms 이웃 그래프 확장 + 누락 방 경고 로그" 추가 (코드-온리, 직렬화 이름 무변경, ~30줄).

## BUG-2: spr_4 보스전 중 위쪽 문 미차단

1. **증상**: spr_4(ThreeWitch 보스전) 중 북쪽 문이 막히지 않음.
2. **재현 경로**: spr_4 진입 → BossBattleTrigger로 전투 시작 → 북쪽 문으로 도주 시도.
3. **의심 파일/클래스**: `MapNode.cs`(Update의 isTrigger 토글, `IsBossBattleLocked`), `BossManager.cs`(IsBossActive 설정 시점), `BossBattleTrigger`, `ThreeWitchCombat.cs`, `spr_4.prefab`(MapNode_4toe: 북쪽, BoxCollider2D 6×1, y=+8.5).
4. **코드 근거(현재까지)**: 북쪽 MapNode는 `nextRoom=Ending`(비null)이라 보스 잠금이 안 걸리면 isTrigger=true(통과 가능). 잠금은 `BossManager.IsBossActive` 또는 "Boss" 태그/BossCombatBase 스캔(0.2s 캐시)에 의존. 남쪽 문과 동일 로직이므로 "북쪽만" 뚫린다면 콜라이더 크기/위치(6×1, y=8.5)가 통로를 다 못 덮거나, 벽 타일 갭 문제 가능성.
5. **코드만으로 수정 가능?**: 미정 — 원인이 콜라이더 기하라면 프리팹 수정 필요.
6. **에디터 확인 필요?**: 필요 — 보스전 중 북쪽 문 isTrigger 상태와 콜라이더 범위를 Scene 뷰에서 실측.
7. **권장 다음 행동**: BUG-1 처리 후, 보스전 중 MapNode 상태 로그(임시 진단) 추가 여부 결정.
8. **위험도**: 진단 로그 = 낮음.
9. **Codex 태스크**: 보류 (에디터 실측 선행).

## BUG-3: 필드 몬스터가 문을 무시

1. **증상**: 몬스터가 방 문(통로)을 그대로 통과.
2. **코드 근거**: `MapNode.Update()`는 평상시 `isTrigger=true`(플레이어 방 전환용) → 트리거 상태의 문은 **어떤 물리 충돌도 막지 않음**. 몬스터 차단 로직 자체가 부재. 구조적 원인 확인됨.
3. **의심 파일**: `MapNode.cs`, `EnemyMovement.cs`, 물리 레이어 매트릭스(ProjectSettings).
4. **코드만으로 수정 가능?**: 부분 — "Enemy 레이어만 막는 별도 차일드 콜라이더"는 프리팹 수정 필요. 코드-온리 대안: EnemyMovement에 방 경계 클램프 추가(방 크기/현재 방 정보 필요).
5. **에디터 확인 필요?**: 레이어/프리팹 방식 선택 시 필요 → 오너 결정 필요.
6. **위험도**: 중간 (물리 정책 변경).

## BUG-4: 문 근처 찌르기 넉백 시 몬스터 수직 튕김

1. **증상**: 문 근처에서 몬스터를 찌르면 위로/문 너머로 튕겨나감.
2. **의심 파일**: `EnemyCombat.cs`/`EnemyStatusController.cs`(넉백 적용), `PlayerAttackSystem.Melee.cs`, `CombatInputHelper.cs`(**작업 트리 dirty — 미커밋 변경 존재, 내용 확인 필요**), MapNode 트리거(충돌 미차단) 연쇄.
3. **가설**: BUG-3과 동일 근원(문이 트리거라 물리 차단 없음) + 넉백 벡터가 벽 노멀과 상호작용. BUG-3 해결 시 함께 완화될 가능성.
4. **상태**: 미조사. BUG-3 원인 확정 후 재평가.

## BUG-5: 보스 투사체 콜라이더/판정 불일치

1. **의심 파일**: `BossProjectile/*.cs` (BossProjectile, StainedSwordProjectile, AquaRay, BriefCandleRay, ElectricLaserRay, BedimmedWall 등), 각 투사체 프리팹의 콜라이더 실측 크기 vs 스프라이트.
2. **상태**: 미조사. 프리팹 콜라이더 수치 read-only 감사 예정.

## BUG-6: 필드 몬스터 이동 이상

1. **의심 파일**: `EnemyMovement.cs`, `EnemyStatusController.cs`(상태이상에 의한 이동 개입), `Spawner.cs`.
2. **상태**: 미조사. BUG-3/4와 근원 공유 여부 확인 후 착수 (마지막 순서).
