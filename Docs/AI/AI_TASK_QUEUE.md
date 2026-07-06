# AI_TASK_QUEUE

> 모드: **BUG-STABILIZATION-FIRST** (2026-07-06)
> 모든 리팩토링 태스크는 버그 수정/진단 하드닝의 일부로 정당화되지 않는 한 착수 금지.

## 활성 (버그 우선)

| # | 태스크 | 대상 버그 | 상태 | 비고 |
|---|---|---|---|---|
| T-101 | BUG-1 read-only 감사 (사망→재시작 방 미로드) | BUG-1 | **완료** | 근본 원인 확인: allMapRooms 누락 (aut_3/Ending/spr_1 BOSS) |
| T-102 | BUG-1 오너 결정: **C안 확정** (B 즉시 + A 에디터 병행) | BUG-1 | **완료** | 결정 카드: AI_DECISION_LOG #2026-07-06-1 |
| T-103 | RoomManager allMapRooms 도달 그래프 확장 + 미매칭 LogError 승격 | BUG-1 | **완료** | 커밋 `7322560`, 순수 추가 63줄 |
| T-109 | [오너 에디터] A안 데이터 보수 | BUG-1 | **완료** | 오너가 allMapRooms 보수 + spr_4 문 → sum_1 직접 반영 |
| T-110 | [오너 에디터] BUG-1 재현 검증 | BUG-1 | **완료** | 여러 방 사망 → 맵 정상 로드 확인 (2026-07-06) |
| T-104 | spr_4 보스전 문 잠금 진단 | BUG-2 | **완료** | 근본 원인: SetBlockades가 MapNode 전체 비활성화, 차단벽 부재. 결정 카드 #2026-07-06-4 |
| T-111 | (승인 시) BossBattleTrigger.SetBlockades MapNode 비활성화 제거 | BUG-2 | 대기 | 코드-온리 ~5줄, 수정 후 오너 플레이테스트 필요 |
| T-105 | 몬스터-문 차단 정책 결정 (레이어/콜라이더 vs 이동 클램프) | BUG-3 | 대기 | 오너 결정 필요 (#2026-07-06-6 옵션 포함) |
| T-106 | 넉백 경로 감사 | BUG-4 | **완료** | 원인: MovePosition 순간이동, 충돌검사 무. 결정 카드 #2026-07-06-6 |
| T-107 | 보스 투사체 판정 코드 감사 | BUG-5 | **완료(코드측)** | 태그 게이트로 정밀 히트박스 미적용 확인. 프리팹 실측은 에디터 필요. 결정 카드 #2026-07-06-7 |
| T-108 | EnemyMovement 감사 | BUG-6 | **완료** | 후보 3건 (셀프넉백 핑퐁/기준점 불일치/벽회피 부재). 오너 증상 구체화 대기 |
| T-111 | BossBattleTrigger.SetBlockades MapNode 비활성화 제거 | BUG-2 | **완료** | 커밋 `c878b06`. 오너 플레이테스트 대기 |
| T-112 | (승인 시) 몬스터 넉백 rb.Cast 클램프 (+옵션: 방 경계 클램프) | BUG-4(+3) | 대기 | 결정 카드 #2026-07-06-6 |

## 보류 — BLOCKED_BY_EXISTING_GAMEPLAY_BUGS

| 태스크 | 출처 | 상태 |
|---|---|---|
| RoomManager 분리(split) 계속 진행 (`refactor/roommanager-split` 브랜치 990f3f9 이후 작업) | REFACTOR_CODEX_20260706.md | **BLOCKED_BY_EXISTING_GAMEPLAY_BUGS** |
| 스파이크 커밋 2558003 (RoomManager 프리로드 중복 제거) 검토/부분 체리픽 | spike/refactor-fable-20260706 | **BLOCKED_BY_EXISTING_GAMEPLAY_BUGS** (통째 체리픽 금지 유지) |
| 일반 코스메틱/주석 리팩토링 전반 | /refactor 스킬 흐름 | **BLOCKED_BY_EXISTING_GAMEPLAY_BUGS** |
| LatentThorn 중복 정리 | 메모리(spike-refactor-branch-20260706) | **BLOCKED_BY_EXISTING_GAMEPLAY_BUGS** |
