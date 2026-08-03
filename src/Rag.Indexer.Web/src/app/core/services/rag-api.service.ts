import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';

import type {
  CollectionInfo,
  CollectionsResponse,
  ContextResponse,
  DirectoryListing,
  IndexProgress,
  IndexResponse,
  LogsResponse,
  RepositoriesResponse,
  ScanResponse,
  SearchResponse
} from '../models/rag.models';

@Injectable({ providedIn: 'root' })
export class RagApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  search(query: string, limit: number): Observable<SearchResponse> {
    return this.http.post<SearchResponse>(`${this.baseUrl}/api/search`, { query, limit });
  }

  getContext(
    query: string,
    limit: number,
    fileFilter?: string,
    symbolTypeFilter?: string
  ): Observable<ContextResponse> {
    const body = { query, limit };
    return this.http.post<ContextResponse>(`${this.baseUrl}/api/context`, {
      ...body,
      ...(fileFilter ? { fileFilter } : {}),
      ...(symbolTypeFilter ? { symbolTypeFilter } : {})
    });
  }

  indexRepository(repositoryPath: string): Observable<IndexResponse> {
    return this.http.post<IndexResponse>(`${this.baseUrl}/api/index`, { repositoryPath });
  }

  getIndexingProgress(): Observable<IndexProgress> {
    return this.http.get<IndexProgress>(`${this.baseUrl}/api/indexing/progress`);
  }

  scanRepository(repositoryPath: string): Observable<ScanResponse> {
    return this.http.post<ScanResponse>(`${this.baseUrl}/api/scan`, { repositoryPath });
  }

  listCollections(): Observable<CollectionsResponse> {
    return this.http.get<CollectionsResponse>(`${this.baseUrl}/api/collections`);
  }

  getCollectionInfo(name: string): Observable<CollectionInfo> {
    return this.http.get<CollectionInfo>(`${this.baseUrl}/api/collections/${encodeURIComponent(name)}`);
  }

  getLogs(source?: string, limit?: number): Observable<LogsResponse> {
    const params: string[] = [];
    if (source) params.push(`source=${encodeURIComponent(source)}`);
    if (limit) params.push(`limit=${limit}`);
    const query = params.length > 0 ? `?${params.join('&')}` : '';
    return this.http.get<LogsResponse>(`${this.baseUrl}/api/logs${query}`);
  }

  listDirectories(path?: string): Observable<DirectoryListing> {
    const query = path ? `?path=${encodeURIComponent(path)}` : '';
    return this.http.get<DirectoryListing>(`${this.baseUrl}/api/directories${query}`);
  }

  getRepositories(): Observable<RepositoriesResponse> {
    return this.http.get<RepositoriesResponse>(`${this.baseUrl}/api/repositories`);
  }
}
