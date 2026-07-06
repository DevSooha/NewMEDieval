# AI_BUG_REGISTRY

> 모드: **BUG-STABILIZATION-FIRST** (2026-07-06 전환)
> 원칙: 진단 → 근본 원인 → 최소 수정. 코스메틱 리팩토링 금지.

## 요약표

| ID | 증상 | 상태 | 우선순위 | 코드만으로 수정 | 에디터 확인 |
|---|---|---|---|---|---|
| BUG-1 | 사망/재시작 후 매니저·플레이어·카메라·UI만 있고 방/맵 없음 | **해결됨 (오너 검증 완료 2026-07-06)** | - | 완료 | 완료 |
| BUG-2 | spr_4 보스전 중 위쪽 문이 안 막힘 | **수정 커밋 완료** `c878b06` (오너 승인) | - | 완료 | 플레이테스트 검증 대기 |
| BUG-3 | 필드 몬스터가 방 문을 무시하고 통과 | 원인 구조 확인됨 (정책 결정 대기) | 2 | 부분 가능 | 정책 선택 필요 |
| BUG-4 | 문 근처에서 찌르기 넉백 시 몬스터가 위로 튕겨 나감 | **근본 원인 확인됨 (결정 대기)** | 2 | **가능** (넉백 클램프) | 수정 후 플레이테스트 |
| BUG-5 | 보스 투사체 콜라이더/판정 범위 불일치 | 원인 후보 2건 확인 | 3 | 부분 가능 | 필요 (프리팹 콜라이더 실측) |
| BUG-6 | 필드 몬스터 이동이 이상함 | 원인 후보 확인 (BUG-4와 근원 공유) | 4 | 부분 가능 | 필요 (증상 구체화) |

---

## 기획 대기 항목 (버그 아님 — 오너 확인 2026-07-06)

- **좌표 혼재(같은 좌표에 복수 방)**: 조건부(스토리 진행별) 방 연결 예정이나 linear 스토리 시스템 기획 미완으로 진행 불가. 코드/데이터로 임의 정리 금지.
- **Ending 방**: 알파테스트용 레거시. 오너가 spr_4 north 문을 sum_1로 직접 수정함. 혼재 구조 정리는 기획 확정 후.
- **aut_3**: 현재 spr_6 위쪽 연결은 spr_7이며 aut_3은 실제 진입 불가 상태(추후 연결 예정).

## BUG-1: 사망/재시작 후 방/맵 미로드 — **해결됨**

- 코드 안전망: 커밋 `7322560` (도달 그래프 BFS 자동 보강 + 매칭 실패 LogError).
- 데이터 보수: 오너가 에디터에서 수행 (allMapRooms 보수 + spr_4 문 → sum_1).
- 검증: 오너가 여러 방에서 사망 → 맵 정상 로드 확인 (2026-07-06).

### (해결 전 감사 기록)

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

## BUG-2: spr_4 보스전 중 위쪽 문 미차단 — 근본 원인 확인됨 (결정 대기)

1. **증상**: spr_4(ThreeWitch 보스전) 중 북쪽 문이 막히지 않음.
2. **재현 경로**: spr_4 진입 → BossBattleTrigger "Fight the boss?" → Yes → 북쪽 통로로 걸어 나감.
3. **의심 파일/클래스**: `BossBattleTrigger.cs`의 `SetBlockades()`, `MapNode.cs`의 Update isTrigger 토글, `spr_4.prefab`(blockadeParent 배선).
4. **코드 근거 (확정)**:
   - `BossBattleTrigger.SetBlockades(true)`는 `blockadeParent` 자식 중 MapNode를 **SetActive(false)**, 그 외(차단벽)를 SetActive(true) 처리.
   - 그런데 **spr_4의 blockadeParent는 "MapNodes" 컨테이너 자체**이고 자식 4개가 전부 MapNode → 보스전 시작 시 **4개 문 오브젝트(콜라이더 포함)가 전부 꺼지고, 활성화될 차단벽은 0개**.
   - MapNode가 꺼지면 `MapNode.Update`의 보스 잠금 solid 전환(isTrigger=false)도, "You cannot flee!" 안내도, 물리 차단도 전부 사라짐 → 통로가 완전 개방.
   - 동일 배선이 `spr_7.prefab`, `aut_3.prefab`에도 존재 → **모든 보스방 공통 결함**. spr_4 북쪽만 눈에 띈 것은 다른 통로가 지형상 덜 노출되기 때문으로 추정. (`sum_3.prefab`은 blockadeParent 자식이 0개라 SetBlockades가 아예 no-op.)
   - MapNode 자체는 보스 잠금 시 solid로 바뀌어 통로를 막도록 설계되어 있음 — SetBlockades의 MapNode 비활성화가 이 설계를 무력화하는 모순.
5. **코드만으로 수정 가능?**: **가능.** `SetBlockades()`에서 MapNode 자식은 비활성화하지 않고 항상 활성 유지(차단은 MapNode의 solid 전환이 담당). 프리팹/씬 무수정.
6. **에디터 확인 필요?**: 수정 자체는 불필요. 수정 후 보스전 플레이테스트 필요(문 차단 + "You cannot flee!" + 전투 종료 후 문 재개방 + 전투 중 투사체가 solid 문과 상호작용하는지).
7. **권장 다음 행동**: 결정 카드 #2026-07-06-4 승인 시 즉시 수정 커밋.
8. **위험도**: 낮음~중간 — 모든 보스방 문 동작이 일괄 변경됨. 원래 의도(문을 아예 숨기는 연출?)가 있었을 가능성은 낮지만 배제 못 함.
9. **Codex 태스크**: `BossBattleTrigger.SetBlockades`에서 MapNode 분기 수정 (~5줄).

## BUG-3: 필드 몬스터가 문을 무시

1. **증상**: 몬스터가 방 문(통로)을 그대로 통과.
2. **코드 근거**: `MapNode.Update()`는 평상시 `isTrigger=true`(플레이어 방 전환용) → 트리거 상태의 문은 **어떤 물리 충돌도 막지 않음**. 몬스터 차단 로직 자체가 부재. 구조적 원인 확인됨.
3. **의심 파일**: `MapNode.cs`, `EnemyMovement.cs`, 물리 레이어 매트릭스(ProjectSettings).
4. **코드만으로 수정 가능?**: 부분 — "Enemy 레이어만 막는 별도 차일드 콜라이더"는 프리팹 수정 필요. 코드-온리 대안: EnemyMovement에 방 경계 클램프 추가(방 크기/현재 방 정보 필요).
5. **에디터 확인 필요?**: 레이어/프리팹 방식 선택 시 필요 → 오너 결정 필요.
6. **위험도**: 중간 (물리 정책 변경).

## BUG-4: 문 근처 찌르기 넉백 시 몬스터 수직 튕김 — 근본 원인 확인됨 (결정 대기)

1. **증상**: 문 근처에서 몬스터를 찌르면 위로/문 너머로 튕겨나감.
2. **코드 근거 (확정)**:
   - 근접 공격(`PlayerAttackSystem.Melee.cs:37`)은 **넉백을 주지 않는다** — 데미지만.
   - 실제 튕김의 주체는 `EnemyCombat.ApplySelfKnockback`(EnemyCombat.cs:90): 몬스터가 플레이어와 **충돌하는 순간** 플레이어 반대 방향으로 `selfKnockbackPixels/32 = 2유닛`을 **`rb.MovePosition` 단발 순간이동**. 찌르려면 붙어야 하므로 "찌르면 튕긴다"로 관측됨. 플레이어가 아래에서 접근하면 방향 = 위 → 문틈으로 순간이동.
   - `EnemyStatusController.ApplyKnockback`(EnemyStatusController.cs:223, 포션 넉백)도 동일하게 MovePosition 순간이동 — **벽/충돌 검사 전무**.
   - 대조: Player 넉백은 Kinematic 전환 + `rb.Cast` 벽 검사(안전 패턴이 이미 프로젝트에 존재).
   - 문이 트리거(BUG-3)라 문틈에는 어차피 콜라이더가 없어 순간이동이 그대로 통과.
3. **코드만으로 수정 가능?**: **가능.** 두 넉백 경로에 `rb.Cast` 기반 이동거리 클램프(Obstacle/Wall 대상) 적용. Player의 기존 패턴 준용. 직렬화 필드 무변경.
4. **위험도**: 낮음~중간 (전투 체감 변화). 결정 카드 #2026-07-06-6.

## BUG-3: 필드 몬스터가 문을 무시 — 정책 결정 대기 (기존 분석 유지)

- 문은 평상시 `isTrigger=true`라 몬스터 차단 장치가 구조적으로 부재.
- 선택지: (a) 방 프리팹에 Enemy 전용 차단 콜라이더 추가(에디터 작업), (b) 코드-온리로 몬스터를 현재 방 사각형 경계 안에 클램프(문틈 진입은 허용되나 방 밖 이탈 방지), (c) a+b.
- BUG-4 클램프와 같은 커밋 레이어로 묶을 수 있음.

## BUG-5: 보스 투사체 콜라이더/판정 불일치 — 원인 후보 2건

1. **[코드] 플레이어 쪽 판정이 본체 캡슐 전체**: `BossProjectile.OnTriggerEnter2D`(BossProjectile.cs:96)는 `other.CompareTag("Player")` 게이트를 거치는데, 정밀 판정용 `CombatTargetHitbox`(본체의 0.6배 박스)는 **자식 오브젝트가 Untagged**라 이 게이트를 통과하지 못함 → 투사체는 항상 **큰 본체 캡슐**에 맞는다. 근접 판정(0.6배)과 투사체 판정(1.0배 캡슐)의 이중 기준 = "판정이 후하다"는 체감과 일치.
2. **[에셋] 투사체 프리팹별 콜라이더 vs 스프라이트 크기**: 에디터 실측 필요.
3. **수정 방향(후보)**: BossProjectile 계열이 CombatTargetHitbox를 우선 조회하도록 변경(코드-온리) — 단, 명중 판정이 전반적으로 좁아져 보스전 난이도 체감이 변함 → 오너 결정 필요.

## BUG-6: 필드 몬스터 이동 이상 — 원인 후보 (증상 구체화 필요)

1. **셀프 넉백 핑퐁**: 플레이어와 닿을 때마다 2유닛 순간이동(BUG-4와 동일 코드) → 접촉 시 몬스터가 덜컥덜컥 튀는 움직임.
2. **판정 기준점 불일치**: `EnemyMovement.CheckForPlayer`는 `detectionPoint` 기준 거리(1f attackRange), `Chase`는 `transform` 기준 거리 → 상태 전환 경계에서 Idle/Chasing/Attacking 왕복 떨림 가능.
3. **벽 회피 부재**: Chase가 플레이어 방향 직선 속도만 설정 — 벽에 걸리면 비비는 움직임(사양일 수 있음).
4. **다음 행동**: 오너에게 "이상함"의 구체 증상 확인(튐/떨림/벽비빔/이탈 중 무엇인지) 후 수정 범위 결정.
