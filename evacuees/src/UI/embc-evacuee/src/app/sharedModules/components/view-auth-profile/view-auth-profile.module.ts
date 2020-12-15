import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';

import { ViewAuthProfileRoutingModule } from './view-auth-profile-routing.module';
import { ViewAuthProfileComponent } from './view-auth-profile.component';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatTabsModule } from '@angular/material/tabs';
import { ReviewModule } from '../../components/review/review.module';
import { EvacuationCardComponent } from '../../components/evacuation-card/evacuation-card.component';

import { MatIconModule } from '@angular/material/icon';


@NgModule({
  declarations: [
    ViewAuthProfileComponent,
    EvacuationCardComponent
  ],
  imports: [
    CommonModule,
    ViewAuthProfileRoutingModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatTabsModule,
    ReviewModule,
    MatIconModule
  ]
})
export class ViewAuthProfileModule { }
