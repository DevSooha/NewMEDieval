# 버그 안정화 요약 (2026-07-06, 브랜치 bugfix/stabilize-core-loop-20260706)

코어 루프를 깨던 버그 6건을 수정했습니다. 전 커밋 `dotnet build Assembly-CSharp.csproj` 통과, 플레이테스트 검증 완료.
씬/프리팹 구조 변경 없음(데이터 보수 2건 제외, 아래 EDITOR_DATA_FIXES 문서 참조). 직렬화 필드/클래스명 변경 없음 — 기존 프리팹 배선 그대로 동작합니다.

## BUG-1: 사망/재시작 후 방/맵이 로드되지 않음 — 해결

- **원인**: 재시작 복원(`RoomManager.RefreshRoomState`)은 `allMapRooms` 좌표 검색에만 의존하는데, 목록에 일부 방(aut_3, Ending, sum_1, sum_2)이 누락 → 그 방에서 재시작하면 방이 하나도 스폰되지 않음. 평상시 이동은 이웃 참조로 동작해 누락이 드러나지 않았음.
- **수정**: `7322560` — Start에서 RoomData 이웃 + 프리팹 MapNode.nextRoom을 BFS 순회해 누락 방 자동 보강(경고 출력). 매칭 실패를 debugLogs와 무관한 LogError로 승격. + 데이터 보수(allMapRooms 보강, `bd159cb`/`d51b43f`).
- **부가**: 자동 보강 경고가 뜨면 allMapRooms 데이터가 빠진 것이니 채워 넣으면 됩니다 (`f1f1aaa`에서 경고를 세션당 1회 요약으로 완화).

## BUG-2: spr_4 보스전 중 위쪽 문이 안 막힘 — 해결

- **원인**: `BossBattleTrigger.SetBlockades`가 보스전 시작 시 blockadeParent("MapNodes")의 문 오브젝트를 전부 비활성화하는데 대신 세울 차단벽이 없어 통로가 완전 개방됨 (spr_4/spr_7/aut_3 공통).
- **수정**: `c878b06` — MapNode는 보스전 중에도 활성 유지. 차단은 원래 설계대로 `MapNode.Update`의 solid 전환(isTrigger=false)이 담당 ("You cannot flee!" 안내 포함). 모든 보스방에 일괄 적용.

## BUG-3: 필드 몬스터가 문을 걸어서 통과 — 1차 해결

- **원인**: 문은 평상시 트리거 콜라이더라 몬스터를 물리적으로 막는 장치가 없음.
- **수정**: `737ea78` — `EnemyMovement.FixedUpdate`에서 몬스터 위치를 방 그리드 셀의 플레이 영역으로 상시 클램프.
- **잔여**: 문틈에 "서 있는 것"까지 막으려면 방 프리팹에 Enemy 전용 콜라이더 필요 (후속 작업).

## BUG-4: 문 근처에서 몬스터가 넉백으로 튕겨 나감 — 해결

- **원인**: 몬스터 넉백 2경로(`EnemyCombat.ApplySelfKnockback` 접촉 넉백, `EnemyStatusController.ApplyKnockback` 포션 넉백)가 `rb.MovePosition` 단발 순간이동으로 벽/문을 통과함. 근접 공격 자체는 넉백을 주지 않으며, "찌르면 튕김"의 실체는 접촉 셀프 넉백이었음.
- **수정**: `8cbf85a` — 충돌 매트릭스 기반 `rb.Cast` 클램프 + 방 셀 경계 클램프 (Player 넉백의 기존 안전 패턴 준용). `1880a5f` — 무시되던 duration 파라미터대로 분할 이동 복구. `2fdaaa9` — 접촉 넉백도 0.18초 분할 이동.

## BUG-5: 보스 투사체 판정이 후함 — 해결(코드측)

- **원인**: 정밀 히트박스(`CombatTargetHitbox`, 본체 0.6배)가 Untagged 자식이라 `CompareTag("Player")` 게이트를 통과하지 못해, 투사체가 항상 큰 본체 캡슐에 맞고 있었음.
- **수정**: `a78d2e9` — 히트박스가 있으면 본체 명중을 양보하고 히트박스 명중만 유효 처리 (판정 너프가 아니라 기존 0.6배 설계 복구). 적용: BossProjectile(공용 베이스), StainedSwordProjectile, FinalBossBedimmedWallProjectile. 레이/지속형 5종과 Masque(포획 기믹)는 의도적으로 제외 — 확대 여부는 밸런스 판단 후.

## BUG-6: 필드 몬스터 이동이 이상함 — 해결

- **원인**: ① 접촉 시 2유닛 순간이동 점프(BUG-4와 동일 코드), ② 공격 즉발 종료 후 쿨다운 1초 동안 Idle 프리즈 → 사거리 경계에서 stop-go 반복.
- **수정**: `2fdaaa9` — 분할 이동 + 사거리 히스테리시스(추격 진입 1.0×attackRange / 정지 후 이탈 1.2×).

## 안정성 보강 (플레이테스트 후속)

- `4a814d0`/`c8c1e6f`: 방 전환 실패 시 전체 롤백 — 막힌 통로나 벽 바깥 착지 시 카메라·플레이어·방 상태가 갈라지지 않도록 시작 상태로 복원 (`Player.IsOnWalkableGround()` 신설).
- `0f5178b`: 씬 로드마다 제조/인게임 메뉴 패널 강제 초기화 — 프리팹에 패널이 켜진 채 저장돼도 화면을 가리지 않음.
- `8ee62f0`: `Singleton<T>` teardown 중 lazy 생성 차단 — 플레이 종료 시 "Player (Singleton)" 잔류 오브젝트 경고 수정.
- `5a2515b`/`0db20bf`/`716ac66`: QA 테스트 콘솔 (PLAYTEST_GUIDE 문서 참조).

## 팀원 주의사항

- `Docs/AI/`는 AI 작업 로그(로컬 전용)로 gitignore 처리 — 공유되지 않는 것이 정상입니다. 팀 공유 문서는 `Docs/Team/`.
- QA 콘솔(`Assets/Scripts/ForDebugging_HG/BugTestConsole.cs`)은 `UNITY_EDITOR || DEVELOPMENT_BUILD` 전용 — 릴리즈 빌드에서는 컴파일 자체가 제외되며, 씬/프리팹 배치 없이 자동 생성되므로 프로덕션 게임 루프에 노출되지 않습니다.
- 새 스크립트 파일은 `.meta`와 함께 커밋되어 있어 GUID 충돌 없이 받을 수 있습니다.
