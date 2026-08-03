export interface SearchResult {
  id: string;
  project: string;
  filePath: string;
  symbolType: string;
  symbolName: string;
  namespace: string;
  content: string;
  score: number;
}

export interface SearchResponse {
  results: SearchResult[];
}

export interface ContextResponse {
  results: SearchResult[];
  aggregatedContext: string | null;
}

export interface IndexResponse {
  message: string;
  path: string;
}

export interface ScanResponse {
  totalFiles: number;
  newOrModified: number;
  deleted: number;
  unchanged: number;
  needsIndexing: boolean;
  files: string[];
}

export interface CollectionsResponse {
  collections: string[];
}

export interface CollectionInfo {
  name: string;
  status: string;
  optimizerStatus: string;
  segmentsCount: number;
  pointsCount: number;
  indexedVectorsCount: number;
  vectorSize: number;
  distance: string;
}

export interface LogEntry {
  timestamp: string;
  level: 'DEBUG' | 'INFO' | 'WARNING' | 'ERROR';
  source: string;
  message: string;
  exception?: string | null;
}

export interface LogsResponse {
  entries: LogEntry[];
}

export interface IndexProgress {
  isRunning: boolean;
  phase: string;
  rootFolder: string | null;
  filesToIndex: number;
  filesToDelete: number;
  filesIndexed: number;
  filesDeleted: number;
  chunksIndexed: number;
  totalChunks: number;
  currentFile: string | null;
  totalFiles: number;
  filesDone: number;
  remainingFiles: number;
  percentComplete: number;
  elapsedSeconds: number;
  estimatedRemainingSeconds: number | null;
  errors: string[];
  startedAt: string | null;
  completedAt: string | null;
  summary: string | null;
}

export interface DirectoryListing {
  path: string;
  parent: string | null;
  directories: string[];
}

export interface IndexedRepository {
  path: string;
  project: string;
  last_indexed: string;
  file_count: number;
}

export interface RepositoriesResponse {
  repositories: string[];
  indexedRepositories: IndexedRepository[];
}

export interface ApiErrorBody {
  error?: string;
}
