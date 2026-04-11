{{- define "cnip.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "cnip.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name (include "cnip.name" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}

{{- define "cnip.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" -}}
{{- end -}}

{{- define "cnip.labels" -}}
helm.sh/chart: {{ include "cnip.chart" . }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}

{{- define "cnip.configMapName" -}}
{{ include "cnip.fullname" . }}-config
{{- end -}}

{{- define "cnip.imageRef" -}}
{{- $g := index .root.Values "global" | default dict -}}
{{- $gReg := index $g "imageRegistry" | default "" | trimSuffix "/" -}}
{{- $acr := .root.Values.acrLoginServer | default "" | trimSuffix "/" -}}
{{- $prefix := $acr | default $gReg -}}
{{- $repo := .repository -}}
{{- $tag := .tag | toString -}}
{{- if contains "/" $repo -}}
{{- printf "%s:%s" $repo $tag -}}
{{- else if $prefix -}}
{{- printf "%s/%s:%s" $prefix $repo $tag -}}
{{- else -}}
{{- printf "%s:%s" $repo $tag -}}
{{- end -}}
{{- end -}}

{{- define "cnip.apiImage" -}}
{{ include "cnip.imageRef" (dict "root" . "repository" .Values.api.image.repository "tag" .Values.api.image.tag) }}
{{- end -}}

{{- define "cnip.workerImage" -}}
{{ include "cnip.imageRef" (dict "root" . "repository" .Values.worker.image.repository "tag" .Values.worker.image.tag) }}
{{- end -}}

{{- define "cnip.aiWorkerImage" -}}
{{ include "cnip.imageRef" (dict "root" . "repository" .Values.aiWorker.image.repository "tag" .Values.aiWorker.image.tag) }}
{{- end -}}

{{- define "cnip.frontendImage" -}}
{{ include "cnip.imageRef" (dict "root" . "repository" .Values.frontend.image.repository "tag" .Values.frontend.image.tag) }}
{{- end -}}

{{- define "cnip.keyVault.volumes" -}}
- name: secrets-store-inline
  csi:
    driver: secrets-store.csi.k8s.io
    readOnly: true
    volumeAttributes:
      secretProviderClass: {{ .Values.keyVault.secretProviderClassName | quote }}
{{- end -}}

{{- define "cnip.keyVault.volumeMounts" -}}
- name: secrets-store-inline
  mountPath: /mnt/secrets-store
  readOnly: true
{{- end -}}

{{- define "cnip.keyVault.initContainers" -}}
{{- $ic := index .Values.keyVault "initContainer" | default dict -}}
{{- $img := index $ic "image" | default "busybox:1.36" -}}
- name: secrets-store-wait
  image: {{ $img | quote }}
  command:
    - sh
    - -c
    - |
      i=0
      while [ "$i" -lt 120 ]; do
        if [ -n "$(ls -A /mnt/secrets-store 2>/dev/null)" ]; then
          exit 0
        fi
        sleep 1
        i=$((i + 1))
      done
      echo "timeout: no files under /mnt/secrets-store (Key Vault CSI mount / auth?)" >&2
      exit 1
  volumeMounts:
    {{- include "cnip.keyVault.volumeMounts" . | nindent 4 }}
{{- end -}}
