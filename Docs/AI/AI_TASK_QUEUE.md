# AI_TASK_QUEUE

> 모드: **BUG-STABILIZATION-FIRST** (2026-07-06)
> 모든 리팩토링 태스크는 버그 수정/진단 하드닝의 일부로 정당화되지 않는 한 착수 금지.

## 활성 (버그 우선)

| # | 태스크 | 대상 버그 | 상태 | 비고 |
|---|---|---|---|---|
| T-101 | BUG-1 read-only 감사 (사망→재시작 방 미로드) | BUG-1 | **완료** | 근본 원인 확인: allMapRooms 누락 (aut_3/Ending/spr_1 BOSS) |
| T-102 | BUG-1 오너 결정 대기: 데이터 보수(씬/프리팹) vs 코드-온리 BFS 확장 | BUG-1 | 대기 | 결정 카드: AI_DECISION_LOG #2026-07-06-1 |
| T-103 | (승인 시) RoomManager allMapRooms 이웃 그래프 확장 + 미매칭 경고 강화 | BUG-1 | 대기 | 코드-온리, 직렬화 필드 무변경 |
| T-104 | spr_4 보스전 문 잠금 진단 (에디터 플레이테스트 필요) | BUG-2 | 대기 | 오너 에디터 테스트 필요 |
| T-105 | 몬스터-문 차단 정책 결정 (레이어/콜라이더 vs 이동 클램프) | BUG-3 | 대기 | 오너 결정 필요 |
| T-106 | EnemyCombat/EnemyStatusController 넉백 경로 read-only 감사 | BUG-4 | 예정 | CombatInputHelper.cs dirty 변경 내용 확인 포함 |
| T-107 | 보스 투사체 프리팹 콜라이더 실측 read-only 감사 | BUG-5 | 예정 | |
| T-108 | EnemyMovement read-only 감사 | BUG-6 | 예정 | BUG-3/4 근원 공유 여부 먼저 판단 |

## 보류 — BLOCKED_BY_EXISTING_GAMEPLAY_BUGS

| 태스크 | 출처 | 상태 |
|---|---|---|
| RoomManager 분리(split) 계속 진행 (`refactor/roommanager-split` 브랜치 990f3f9 이후 작업) | REFACTOR_CODEX_20260706.md | **BLOCKED_BY_EXISTING_GAMEPLAY_BUGS** |
| 스파이크 커밋 2558003 (RoomManager 프리로드 중복 제거) 검토/부분 체리픽 | spike/refactor-fable-20260706 | **BLOCKED_BY_EXISTING_GAMEPLAY_BUGS** (통째 체리픽 금지 유지) |
| 일반 코스메틱/주석 리팩토링 전반 | /refactor 스킬 흐름 | **BLOCKED_BY_EXISTING_GAMEPLAY_BUGS** |
| LatentThorn 중복 정리 | 메모리(spike-refactor-branch-20260706) | **BLOCKED_BY_EXISTING_GAMEPLAY_BUGS** |
