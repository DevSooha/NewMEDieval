# AI_GUARDRAILS

> 2026-07-06 BUG-STABILIZATION-FIRST 모드 기준. 모든 AI 에이전트(Fable/Codex) 공통.

## 절대 금지
- `.unity`, `.prefab`, `.asset`, `.meta` 파일 수정 — 오너 명시 승인 없이는 금지.
- MonoBehaviour 클래스 이름 변경 금지.
- 직렬화 필드([SerializeField]/public 인스펙터 필드) 이름 변경 금지.
- 프리팹에 와이어링된 public 필드 이름 변경 금지.
- RoomManager / 보스 시스템 전면 재작성 금지.
- `git add .` 금지 — 커밋 대상 파일을 개별 명시.
- 오너의 기존 dirty 파일(.gitignore, MainCanvas.prefab, FIeld.unity, CombatInputHelper.cs, ProjectSettings.asset)을 커밋에 포함 금지.
- 커밋 `2558003` 통째 체리픽 금지.

## 반드시 멈추고 오너에게 물어볼 것 (한국어 결정 카드)
- 씬/프리팹/ScriptableObject 변경이 필요한 수정.
- 물리 레이어/태그/콜라이더 변경(에디터 설정 영역).
- Unity 에디터 플레이테스트가 필요한 검증.
- 브랜치 상태가 혼란스러울 때.

## 허용 (자율 수행)
- READ-ONLY 감사 (코드/에셋 YAML 읽기, git 조회).
- 작고 되돌리기 쉬운 코드-온리 진단/로그 추가 (직렬화 이름·에셋 무변경).
- Docs/AI 문서 갱신.
- bugfix 브랜치 내 문서/코드 커밋 (버그 1개 또는 진단 레이어 1개당 커밋 1개).

## 커밋 규칙
- 브랜치: `bugfix/stabilize-core-loop-20260706` (base: `hyunseo` + `9f9b391`).
- 한 커밋 = 한 버그 수정 또는 한 진단 레이어.
- 리팩토링은 버그 수정/진단 하드닝의 일부로 정당화될 때만 허용.
