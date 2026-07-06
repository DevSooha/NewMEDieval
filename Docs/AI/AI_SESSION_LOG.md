# AI_SESSION_LOG

## 2026-07-06 — 세션: 버그 안정화 모드 전환 + BUG-1 감사 (Fable)

**모드 전환**: 오너 지시로 BUG-STABILIZATION-FIRST 전환. 일반 리팩토링/RoomManager 분리 작업 전량 BLOCKED_BY_EXISTING_GAMEPLAY_BUGS 처리 (AI_TASK_QUEUE.md).

**브랜치 작업**:
- 시작 상태: `refactor/roommanager-split` (hyunseo + 분리 커밋 3개), dirty 5개 + untracked 다수.
- `bugfix/stabilize-core-loop-20260706` 생성 (base `hyunseo`), `9f9b391` 체리픽 → `e50caf0`. 충돌 없음.
- 오너 dirty 파일 무수정/무커밋 유지.

**BUG-1 READ-ONLY 감사 결과**: 근본 원인 확인.
- 경로: `UIManager.RestartGameRoutine` → `SaveManager.ApplyLoadedData`(또는 `SetRestartPositionToCurrentDoor`) → `RoomManager.restartPointOverride` → 씬 리로드 → `RoomManager.Start` → `RestoreRoutine` → `RefreshRoomState` → `GetRoomDataByCoord`(allMapRooms 선형 검색).
- 유효 allMapRooms 16개에 `aut_3`(1,6)/`Ending`(1,4)/`spr_1 BOSS`(0,1) 누락 → 해당 좌표에서 재시작하면 매칭 실패 → 방 0개 스폰, 경고만 출력(그마저 미커밋 씬 변경으로 debugLogs=0이라 안 보임).
- 부수 발견: spr_1↔spr_1 BOSS, Ending↔sum_1 좌표 중복. spr_4 북쪽 문(nextRoom=Ending) vs spr_4.asset north(sum_1) 불일치 → (1,4) 방 겹침 스폰 가능.

**BUG-2/3 예비 관찰**: MapNode는 평상시 isTrigger=true(몬스터 차단 불가 = BUG-3 구조적 원인), 보스 잠금 시 solid 전환. spr_4 북쪽 문 콜라이더 6×1 @ y+8.5 — 실측 필요.

**산출물**: Docs/AI 6종 신규 작성. 코드 무변경 (읽기 전용 감사만).

**오너 대기 항목**: BUG-1 결정 카드(A/B/C), 에디터에서 debugLogs 재활성화, spr_4 보스전 플레이테스트.

## 2026-07-06 — 세션 계속: BUG-1 C안 실행 (Fable)

- 오너가 결정 카드 #2026-07-06-1에서 **C안(코드 안전망 + 에디터 데이터 보수 병행)** 확정.
- 추가 조사: `spr_1 BOSS` 에셋(guid c9f16b7c...)은 씬/프리팹 어디서도 미참조 — 고아 데이터. `Ending`은 spr_4 프리팹 MapNode.nextRoom으로만 연결(이웃 그래프에 없음) → 순회를 RoomData 이웃 + 프리팹 MapNode.nextRoom 두 채널로 설계.
- 커밋 `7322560` (RoomManager.cs, 순수 추가 63줄): `ExpandAllMapRoomsWithReachableRooms()` BFS 안전망 + `RefreshRoomState` 매칭 실패 시 debugLogs 무관 LogError.
- 오너 에디터 작업 대기: T-109(데이터 보수), T-110(aut_3 사망→재시작 재현 검증).

## 2026-07-06 — 세션 계속: BUG-1 해결 확정 + BUG-2 근본 원인 감사 (Fable)

**오너 보고 반영**:
- BUG-1 검증 완료: 여러 방에서 사망 → 맵 정상 로드 확인. T-109/T-110 완료 처리.
- 오너가 spr_4 문을 sum_1로 직접 수정 (Ending은 알파테스트 레거시).
- 좌표 혼재 구조 = 조건부 스토리 연결 기획 대기 → "기획 대기 항목"으로 분리, AI 임의 정리 금지.
- aut_3은 현재 실제 진입 불가 (spr_6 위쪽은 spr_7).

**BUG-2 READ-ONLY 감사 결과**: 근본 원인 확인.
- `BossBattleTrigger.SetBlockades(true)`는 blockadeParent 자식 중 MapNode를 SetActive(false), 비-MapNode를 SetActive(true) 처리.
- spr_4/spr_7/aut_3의 blockadeParent = "MapNodes" 컨테이너 (자식 전원 MapNode) → 보스전 시작 시 문 4개 전부 꺼짐 + 차단벽 0개 → 통로 완전 개방. MapNode.Update의 보스 잠금 solid 전환 설계와 모순.
- sum_3은 blockadeParent 자식 0개 → SetBlockades 자체가 no-op.
- 수정안(결정 카드 #2026-07-06-4): SetBlockades에서 MapNode는 항상 활성 유지 (~5줄, 코드-온리).

**T-106 부분 진행**: dirty 상태 CombatInputHelper.cs 확인 — 주석/가독성 정리(프로퍼티 추출)만 있고 로직 동일. BUG-4와 무관. 오너 소유 미커밋 변경으로 계속 미접촉.

## 2026-07-06 — 세션 계속: BUG-2 수정 커밋 + T-106/107/108 감사 (Fable)

- 오너가 결정 카드 #2026-07-06-4 **승인** → `c878b06` 커밋: SetBlockades에서 MapNode 자식은 항상 활성 유지 (~9줄). 플레이테스트 대기.
- **T-106 완료 (BUG-4 원인 확정)**: 근접 공격은 넉백 없음. 튕김의 주체는 EnemyCombat.ApplySelfKnockback(접촉 시 2유닛)과 EnemyStatusController.ApplyKnockback(포션) — 둘 다 rb.MovePosition 순간이동, 충돌검사 무. Player 넉백의 rb.Cast 패턴 준용한 클램프 제안 (#2026-07-06-6).
- **T-107 완료 (BUG-5 코드측)**: BossProjectile.OnTriggerEnter2D의 CompareTag("Player") 게이트 → CombatTargetHitbox 자식이 Untagged라 정밀 히트박스(0.6배) 미적용, 본체 캡슐 전체가 판정 (#2026-07-06-7). 프리팹별 콜라이더 실측은 에디터 필요.
- **T-108 완료 (BUG-6 후보)**: ① 셀프넉백 핑퐁(BUG-4 동일 코드), ② CheckForPlayer(detectionPoint 기준)와 Chase(transform 기준) 거리 기준 불일치로 상태 떨림 가능, ③ 벽회피 부재. 오너 증상 구체화 대기.
