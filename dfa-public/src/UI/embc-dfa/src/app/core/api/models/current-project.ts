/* tslint:disable */
/* eslint-disable */
import { ProjectStatusBar } from './project-status-bar';
export interface CurrentProject {
  deadline18Month?: string;
  emcrApprovedAmount?: string;
  estimatedCompletionDate?: string;
  isErrorInStatus?: boolean;
  isHidden?: boolean;
  projectId?: string;
  projectName?: string;
  projectNumber?: string;
  siteLocation?: string;
  stage?: string;
  status?: string;
  statusBar?: Array<ProjectStatusBar>;
  statusColor?: string;
  statusLastUpdated?: string;
}
