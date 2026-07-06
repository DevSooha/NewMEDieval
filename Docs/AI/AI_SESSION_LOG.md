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
