import { Component, OnInit, NgModule, Inject, OnDestroy, ChangeDetectorRef } from '@angular/core';
import {
  UntypedFormBuilder,
  UntypedFormGroup,
  AbstractControl,
  Validators,
  FormsModule,
  FormGroup,
} from '@angular/forms';
import { CommonModule, KeyValue } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { ReactiveFormsModule } from '@angular/forms';
import { FormCreationService } from 'src/app/core/services/formCreation.service';
import { BehaviorSubject, Subscription, mapTo } from 'rxjs';
import { DirectivesModule } from '../../../../core/directives/directives.module';
import { CustomValidationService } from 'src/app/core/services/customValidation.service';
import { ApplicantOption, FileCategory, SupportStatus } from 'src/app/core/api/models';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { CoreModule } from 'src/app/core/core.module';
import { DFAApplicationMainService } from 'src/app/feature-components/dfa-application-main/dfa-application-main.service';
import { DFAApplicationMainDataService } from 'src/app/feature-components/dfa-application-main/dfa-application-main-data.service';
import { AttachmentService } from 'src/app/core/api/services';
import { DFAApplicationStartDataService } from 'src/app/feature-components/dfa-application-start/dfa-application-start-data.service';
import { MatTab } from '@angular/material/tabs';

@Component({
  selector: 'app-supporting-documents',
  templateUrl: './supporting-documents.component.html',
  styleUrls: ['./supporting-documents.component.scss']
})
export default class SupportingDocumentsComponent implements OnInit, OnDestroy {
  insuranceTemplateForm: UntypedFormGroup;
  insuranceTemplateForm$: Subscription;
  rentalAgreementForm: UntypedFormGroup;
  rentalAgreementForm$: Subscription;
  identificationForm: UntypedFormGroup;
  identificationForm$: Subscription;
  insuranceTemplateDataSource = new MatTableDataSource();
  rentalAgreementDataSource = new MatTableDataSource();
  identificationDataSource = new MatTableDataSource();
  supportingDocumentsForm: UntypedFormGroup;
  formBuilder: UntypedFormBuilder;
  supportingDocumentsForm$: Subscription;
  supportingFilesForm: UntypedFormGroup;
  supportingFilesForm$: Subscription;
  formCreationService: FormCreationService;
  showSupportingFileForm: boolean = false;
  supportingFilesDataSource = new MatTableDataSource();
  documentSummaryColumnsToDisplay = [ 'fileName', 'fileDescription', 'fileType', 'uploadedDate', 'icons']
  documentSummaryDataSource = new MatTableDataSource();
  allowedFileTypes = [
    'application/pdf',
    'image/jpg',
    'image/jpeg',
    'image/png',
    'application/msword',
    'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    'application/vnd.ms-powerpoint',
    'application/vnd.openxmlformats-officedocument.presentationml.presentation',
    'application/vnd.ms-excel',
    'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
  ];
  FileCategories = FileCategory;
  showOtherDocuments: boolean = false;
  isResidentialTenant: boolean = false;
  AppOptions = ApplicantOption;

  constructor(
    @Inject('formBuilder') formBuilder: UntypedFormBuilder,
    @Inject('formCreationService') formCreationService: FormCreationService,
    public customValidator: CustomValidationService,
    private dfaApplicationMainService: DFAApplicationMainService,
    private dfaApplicationMainDataService: DFAApplicationMainDataService,
    private attachmentsService: AttachmentService
  ) {
    this.formBuilder = formBuilder;
    this.formCreationService = formCreationService;
    this.isResidentialTenant = (this.dfaApplicationMainDataService.dfaApplicationStart.appTypeInsurance.applicantOption == Object.keys(this.AppOptions)[Object.values(this.AppOptions).indexOf(this.AppOptions.ResidentialTenant)]);
  }

  ngOnInit(): void {
    this.supportingDocumentsForm$ = this.formCreationService
      .getSupportingDocumentsForm()
      .subscribe((supportingDocuments) => {
        this.supportingDocumentsForm = supportingDocuments;
        this.supportingDocumentsForm.get('hasCopyOfARentalAgreementOrLease').setValue(false);
      });

    this.supportingFilesForm$ = this.formCreationService
      .getFileUploadsForm()
      .subscribe((fileUploads) => {
        this.supportingFilesForm = fileUploads;
      });

    this.supportingFilesForm
      .get('addNewFileUploadIndicator')
      .valueChanges.subscribe((value) => this.updateFileUploadFormOnVisibility(this.supportingFilesForm));

    const _supportingFilesFormArray = this.formCreationService.fileUploadsForm.value.get('fileUploads');
      _supportingFilesFormArray.valueChanges
        .pipe(
          mapTo(_supportingFilesFormArray.getRawValue())
          ).subscribe(data => this.supportingFilesDataSource.data = data.filter(x => [this.FileCategories.Financial, this.FileCategories.Identification, Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.TenancyProof)], Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.ThirdPartyConsent)]].indexOf(x.fileType) >= 0 && x.deleteFlag == false));

    this.initInsuranceTemplate();
    this.initRentalAgreement();
    this.initIdentification();

    // subscribe to changes for document summary
    const _documentSummaryFormArray = this.formCreationService.fileUploadsForm.value.get('fileUploads');
    _documentSummaryFormArray.valueChanges
      .pipe(
        mapTo(_documentSummaryFormArray.getRawValue())
        ).subscribe(data => this.documentSummaryDataSource.data = data.filter(x => x.deleteFlag == false));
  }

  initInsuranceTemplate() {
    // set up insurance template
    this.insuranceTemplateForm$ = this.formCreationService
    .getFileUploadsForm()
    .subscribe((fileUploads) => {
      this.insuranceTemplateForm = fileUploads;
      this.insuranceTemplateForm.addValidators([this.validateFormInsuranceTemplate]);
    });

    this.insuranceTemplateForm
      .get('addNewFileUploadIndicator')
      .valueChanges.subscribe((value) => this.updateFileUploadFormOnVisibility(this.insuranceTemplateForm));

    const _insuranceTemplateFormArray = this.formCreationService.fileUploadsForm.value.get('fileUploads');
      _insuranceTemplateFormArray.valueChanges
        .pipe(
          mapTo(_insuranceTemplateFormArray.getRawValue())
          ).subscribe(data => this.insuranceTemplateDataSource.data = data.filter(x => x.fileType == Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.Insurance)] && x.deleteFlag == false));

    // initialize insurance template
    let fileUploads = this.formCreationService.fileUploadsForm.value.get('fileUploads').value;
    if (fileUploads.filter(x => x.fileType === Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.Insurance)]).length > 0) {
      let insuranceFoundIndex = fileUploads.findIndex(x => x.fileType === Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.Insurance)]);
      this.insuranceTemplateForm.get('fileUpload').setValue(fileUploads[insuranceFoundIndex]);
    } else {
      this.initFileUploadForm(this.insuranceTemplateForm, Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.Insurance)]);
    }
  }

  initRentalAgreement() {
    // set up rental agreement
    this.rentalAgreementForm$ = this.formCreationService
    .getFileUploadsForm()
    .subscribe((fileUploads) => {
      this.rentalAgreementForm = fileUploads;
      this.rentalAgreementForm.addValidators([this.validateFormRentalAgreement]);
    });

    this.rentalAgreementForm
      .get('addNewFileUploadIndicator')
      .valueChanges.subscribe((value) => this.updateFileUploadFormOnVisibility(this.rentalAgreementForm));

    const _rentalAgreementFormArray = this.formCreationService.fileUploadsForm.value.get('fileUploads');
      _rentalAgreementFormArray.valueChanges
        .pipe(
          mapTo(_rentalAgreementFormArray.getRawValue())
          ).subscribe(data => this.rentalAgreementDataSource.data = data.filter(x => x.fileType == Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.TenancyProof)] && x.deleteFlag == false));

    // initialize file upload form
    let fileUploads = this.formCreationService.fileUploadsForm.value.get('fileUploads').value;
    if (fileUploads.filter(x => x.fileType === Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.TenancyProof)]).length > 0) {
      let rentalAgreementFoundIndex = fileUploads.findIndex(x => x.fileType === Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.TenancyProof)]);
      this.rentalAgreementForm.get('fileUpload').setValue(fileUploads[rentalAgreementFoundIndex]);
    } else {
      this.initFileUploadForm(this.rentalAgreementForm, Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.TenancyProof)]);
    }
  }

  initIdentification() {
    this.identificationForm$ = this.formCreationService
    .getFileUploadsForm()
    .subscribe((fileUploads) => {
      this.identificationForm = fileUploads;
      this.identificationForm.addValidators([this.validateFormIdentification]);
    });

    this.identificationForm
      .get('addNewFileUploadIndicator')
      .valueChanges.subscribe((value) => this.updateFileUploadFormOnVisibility(this.identificationForm));

    const _identificationFormArray = this.formCreationService.fileUploadsForm.value.get('fileUploads');
      _identificationFormArray.valueChanges
        .pipe(
          mapTo(_identificationFormArray.getRawValue())
          ).subscribe(data => this.identificationDataSource.data = data.filter(x => x.fileType == Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.Identification)] && x.deleteFlag == false));

    // initialize file upload form
    let fileUploads = this.formCreationService.fileUploadsForm.value.get('fileUploads').value;
    if (fileUploads.filter(x => x.fileType === Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.Identification)]).length > 0) {
      let identificationFoundIndex = fileUploads.findIndex(x => x.fileType === Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.Identification)]);
      this.identificationForm.get('fileUpload').setValue(fileUploads[identificationFoundIndex]);
    } else {
      this.initFileUploadForm(this.identificationForm, Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.Identification)]);
    }
  }

  initFileUploadForm(form: UntypedFormGroup, fileCategory: string) {
    form.get('fileUpload').reset();
    form.get('fileUpload.modifiedBy').setValue("Applicant");
    form.get('fileUpload.fileType').setValue(fileCategory);
    form.get('addNewFileUploadIndicator').setValue(true);
    form.get('fileUpload.deleteFlag').setValue(false);
    form.get('fileUpload.applicationId').setValue(this.dfaApplicationMainDataService.dfaApplicationStart.id);
  }

  // Preserve original property order
  originalOrder = (a: KeyValue<number,string>, b: KeyValue<number,string>): number => {
    return 0;
  }

  validateFormInsuranceTemplate(form: FormGroup) {
    let FileCategories = FileCategory;

    let supportingFiles = form.get('fileUploads')?.getRawValue();
    if (supportingFiles?.filter(x => x.fileType === Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.Insurance)] && x.deleteFlag == false).length <= 0) {
      return { noInsuranceTemplate: true };
    }
    return null;
  }

  validateFormRentalAgreement(form: FormGroup) {
    let FileCategories = FileCategory;

    let supportingFiles = form.get('fileUploads')?.getRawValue();
    if (supportingFiles?.filter(x => x.fileType === Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.TenancyProof)] && x.deleteFlag == false).length <= 0) {
      return { noRentalAgreement: true };
    }
    return null;
  }

  validateFormIdentification(form: FormGroup) {
    let FileCategories = FileCategory;

    let supportingFiles = form.get('fileUploads')?.getRawValue();
    if (supportingFiles?.filter(x => x.fileType === Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.Identification)] && x.deleteFlag == false).length <= 0) {
      return { noIdentification: true };
    }
    return null;
  }

  addSupportingFile(): void {
    this.supportingFilesForm.get('fileUpload').reset();
    this.supportingFilesForm.get('fileUpload.modifiedBy').setValue("Applicant");
    this.showSupportingFileForm = !this.showSupportingFileForm;
    this.supportingFilesForm.get('addNewFileUploadIndicator').setValue(true);
    this.supportingFilesForm.get('fileUpload.deleteFlag').setValue(false);
    this.supportingFilesForm.get('fileUpload.applicationId').setValue(this.dfaApplicationMainDataService.dfaApplicationStart.id);
  }

  saveSupportingFiles(): void {
    if (this.supportingFilesForm.get('fileUpload').status === 'VALID') {
      let fileUploads = this.formCreationService.fileUploadsForm.value.get('fileUploads').value;
      this.attachmentsService.attachmentUpsertDeleteAttachment({body: this.supportingFilesForm.get('fileUpload').getRawValue() }).subscribe({
        next: (fileUploadId) => {
          this.supportingFilesForm.get('fileUpload').get('id').setValue(fileUploadId);
          fileUploads.push(this.supportingFilesForm.get('fileUpload').getRawValue());
          this.formCreationService.fileUploadsForm.value.get('fileUploads').setValue(fileUploads);
          this.showSupportingFileForm = !this.showSupportingFileForm;
          if (this.supportingFilesForm.get('fileUpload').get('fileType').value == Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.TenancyProof)])
            this.supportingDocumentsForm.get('hasCopyOfARentalAgreementOrLease').setValue(true);
        },
        error: (error) => {
          console.error(error);
        }
      });
    } else {
      this.supportingFilesForm.get('fileUpload').markAllAsTouched();
    }
  }

  saveRequiredForm(form: UntypedFormGroup): void {
    console.log(form);
    if (form.get('fileUpload').status === 'VALID') {
      let fileUploads = this.formCreationService.fileUploadsForm.value.get('fileUploads').value;
      if (fileUploads.filter(x => x.fileType === form.get('fileUpload.fileType').value).length > 0) {
        this.attachmentsService.attachmentUpsertDeleteAttachment({body: form.get('fileUpload').getRawValue() }).subscribe({
          next: (fileUploadId) => {
            let typeFoundIndex = fileUploads.findIndex(x => x.fileType === form.get('fileUpload.fileType').value);
            fileUploads[typeFoundIndex] = form.get('fileUpload').getRawValue();
            this.formCreationService.fileUploadsForm.value.get('fileUploads').setValue(fileUploads);
          },
          error: (error) => {
            console.error(error);
          }
        });
      } else {
        this.initFileUploadForm(form, form.get('fileUpload.fileType').value);
        this.attachmentsService.attachmentUpsertDeleteAttachment({body: form.get('fileUpload').getRawValue() }).subscribe({
          next: (fileUploadId) => {
            form.get('fileUpload').get('id').setValue(fileUploadId);
            fileUploads.push(form.get('fileUpload').value);
            this.formCreationService.fileUploadsForm.value.get('fileUploads').setValue(fileUploads);
          },
          error: (error) => {
            console.error(error);
          }
        });
      }
    } else {
      console.error(form.get('fileUpload'));
      form.get('fileUpload').markAllAsTouched();
    }
  }

  cancelSupportingFiles(): void {
    this.showSupportingFileForm = !this.showSupportingFileForm;
    this.supportingFilesForm.get('addNewFileUploadIndicator').setValue(false);
  }

  deleteDocumentSummaryRow(element): void {
    var form = (element.fileType == "Insurance" ? this.insuranceTemplateForm :
    (element.fileType == "Identification" ? this.identificationForm :
    (element.fileType == "TenancyProof" ? this.rentalAgreementForm : null)));
    if (form != null) {
      let fileUploads = this.formCreationService.fileUploadsForm.value.get('fileUploads').value;
      let foundIndex = fileUploads.findIndex(x => x.fileType === element.fileType);
      element.deleteFlag = true;
      this.attachmentsService.attachmentUpsertDeleteAttachment({body: element}).subscribe({
        next: (fileUploadId) => {
          fileUploads[foundIndex] = element;
          this.formCreationService.fileUploadsForm.value.get('fileUploads').setValue(fileUploads);
          this.initFileUploadForm(form, element.fileType);
        },
        error: (error) => {
          console.error(error);
        }
      });
    } else if (element.fileType === Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.DamagePhoto)]) {
      this.dfaApplicationMainService.deleteDamagePhoto.emit(element);
    } else if (element.fileType === this.FileCategories.Cleanup) {
      this.dfaApplicationMainService.deleteCleanupLog.emit(element);
    } else {
      let fileUploads = this.formCreationService.fileUploadsForm.value.get('fileUploads').value;
      let index = fileUploads.indexOf(element);
      element.deleteFlag = true;
      this.attachmentsService.attachmentUpsertDeleteAttachment({body: element}).subscribe({
        next: (fileUploadId) => {
          fileUploads[index] = element;
          this.formCreationService.fileUploadsForm.value.get('fileUploads').setValue(fileUploads);
          if (fileUploads.filter(x => x.fileType == Object.keys(this.FileCategories)[Object.values(this.FileCategories).indexOf(this.FileCategories.TenancyProof)])?.length == 0)
            this.supportingDocumentsForm.get('hasCopyOfARentalAgreementOrLease').setValue(false);
          if (this.formCreationService.fileUploadsForm.value.get('fileUploads').value.length === 0) {
            this.supportingFilesForm
              .get('addNewFileUploadIndicator')
              .setValue(false);
          }
        },
        error: (error) => {
          console.error(error);
        }
      });
    }
  }

  updateFileUploadFormOnVisibility(form: UntypedFormGroup): void {
    form.get('fileUpload.fileName').updateValueAndValidity();
    form.get('fileUpload.fileDescription').updateValueAndValidity();
    form.get('fileUpload.fileType').updateValueAndValidity();
    form.get('fileUpload.uploadedDate').updateValueAndValidity();
    form.get('fileUpload.modifiedBy').updateValueAndValidity();
    form.get('fileUpload.fileData').updateValueAndValidity();
  }

  /**
   * Returns the control of the form
   */
  get supportingFilesFormControl(): { [key: string]: AbstractControl } {
    return this.supportingFilesForm.controls;
  }
  get insuranceTemplateFormControl(): { [key: string]: AbstractControl } {
    return this.insuranceTemplateForm.controls;
  }
  get rentalAgreementFormControl(): { [key: string]: AbstractControl } {
    return this.rentalAgreementForm.controls;
  }
  get identificationFormControl(): { [key: string]: AbstractControl} {
    return this.identificationForm.controls;
  }

  ngOnDestroy(): void {
    this.supportingDocumentsForm$.unsubscribe();
    this.supportingFilesForm$.unsubscribe();
    this.insuranceTemplateForm$.unsubscribe();
  }

/**
 * Reads the attachment content and encodes it as base64
 *
 * @param event : Attachment drop/browse event
 */
  setFileFormControl(form: UntypedFormGroup, event: any) {
    const reader = new FileReader();
    reader.readAsDataURL(event);
    reader.onload = () => {
      form.get('fileUpload.fileName').setValue(event.name);
      form.get('fileUpload.fileDescription').setValue(event.name);
      form.get('fileUpload.fileData').setValue(reader.result);
      form.get('fileUpload.contentType').setValue(event.type);
      form.get('fileUpload.fileSize').setValue(event.size);
      form.get('fileUpload.uploadedDate').setValue(new Date());
      console.log(form, this.insuranceTemplateForm);
    };
  }

  public onToggleOtherDocuments() {
    this.showOtherDocuments = !this.showOtherDocuments;
  }
}

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    CoreModule,
    MatTableModule,
    MatCardModule,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    ReactiveFormsModule,
    DirectivesModule,
  ],
  declarations: [SupportingDocumentsComponent]
})
class SupportingDocumentsModule {}
