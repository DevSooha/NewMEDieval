# FABLE_HANDOFF

> 다음 세션의 AI 에이전트가 이 파일부터 읽을 것. (갱신: 2026-07-06)

## 현재 모드
**BUG-STABILIZATION-FIRST.** 일반/코스메틱 리팩토링 전면 중단. RoomManager 분리 작업도 버그 진단/수정에 직접 필요하지 않으면 중단. 상세 규칙: `AI_GUARDRAILS.md`.

## 브랜치 상태
- 작업 브랜치: `bugfix/stabilize-core-loop-20260706` = `hyunseo` + `9f9b391` 체리픽(`e50caf0`).
- `refactor/roommanager-split`(hyunseo + 분리 커밋 3개)은 그대로 보존, 추가 진행 금지.
- `spike/refactor-fable-20260706`의 `2558003`은 통째 체리픽 금지.
- 오너의 미커밋 dirty 파일 5개는 건드리지도 커밋하지도 말 것 (목록: AI_GUARDRAILS.md).

## 버그 현황 (상세: AI_BUG_REGISTRY.md)
- **BUG-1: 해결됨 (오너 검증 2026-07-06)** — 코드 안전망 `7322560` + 오너 데이터 보수(allMapRooms, spr_4 문→sum_1).
- **BUG-2: 근본 원인 확인, 결정 대기** — `BossBattleTrigger.SetBlockades()`가 보스전 시작 시 blockadeParent("MapNodes")의 문 4개를 전부 비활성화, 세울 차단벽은 0개 → 통로 완전 개방. spr_7/aut_3도 동일 배선. 수정안(코드-온리 ~5줄): MapNode는 활성 유지, 차단은 MapNode.Update의 solid 전환에 위임. 결정 카드 #2026-07-06-4.
- BUG-3: 문이 평상시 isTrigger=true라 몬스터를 물리적으로 못 막음 (차단 로직 부재) — 정책 결정 필요.
- BUG-4/5/6: 미조사. 단 CombatInputHelper.cs dirty 변경은 주석 정리로 확인(BUG-4 무관).

## 기획 대기 (오너 확인 — AI가 건드리지 말 것)
- 좌표 혼재(동일 좌표 복수 방) = 조건부 스토리 연결 예정, 기획 미완. 임의 정리 금지.
- Ending 방 = 알파테스트 레거시 (spr_4 연결은 오너가 sum_1로 이미 수정).
- aut_3 = 현재 실제 진입 불가 (spr_6 위쪽은 spr_7).

## 핵심 지식 (재조사 방지)
- 방 데이터: `Assets/Scripts/Field/*.asset` 18개. 좌표 중복: spr_1↔spr_1 BOSS (0,1), Ending↔sum_1 (1,4).
- `allMapRooms` 유효 목록 = GameManager.prefab 13개 + FIeld.unity 오버라이드 [13]=sum_2, [14]=sum_1, [15]=sum_3(중복).
- `spr_4.prefab` 북쪽 문(MapNode_4toe, BoxCollider2D 6×1, local y=+8.5)의 nextRoom은 `Ending`인데 `spr_4.asset`의 north는 `sum_1` — 데이터 불일치.
- `overrideDistance`는 프로젝트 전체에서 전부 0 → 방 배치는 그리드 정합.
- 평상시 방 이동은 RoomData 이웃 참조 기반이라 allMapRooms 누락이 안 드러남. 재시작 경로(`RefreshRoomState` → `GetRoomDataByCoord`)에서만 터짐.
- 미커밋 FIeld.unity 변경에 `debugLogs: 0` 포함 → 콘솔 경고가 꺼져 있음 (오너에게 켜달라고 요청할 것).

## 다음 행동
1. 오너의 BUG-2 결정(#2026-07-06-4) 확인 → 승인 시 T-111 실행.
2. T-106(넉백 경로)/T-107(투사체 콜라이더)/T-108(EnemyMovement) read-only 감사 순차 진행.
