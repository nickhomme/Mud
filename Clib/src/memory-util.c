//
// Created by Nicholas Homme on 1/17/20.
//

#include "memory-util.h"

ptr _store_string(char *val) {
  ptr pointer = malloc(sizeof(char) * (strlen(val) + 1));
  memcpy(pointer, val, sizeof(char) * (strlen(val) + 1));
  return pointer;
}

char* _store_read_string(ptr pointer) {
  return (char*) pointer;
}

ptr _store_char(char val) {
  ptr pointer = malloc(sizeof(char));
  memcpy(pointer, &val, sizeof(char));
  return pointer;
}
char _store_read_char(ptr pointer) {
  return *((char*) pointer);
}

ptr _store_bool(bool val) {
  ptr pointer = malloc(sizeof(bool));
  memcpy(pointer, &val, sizeof(bool));
  return pointer;
}
bool _store_read_bool(ptr pointer) {
  return *((bool*) pointer);
}

ptr _store_float(float val) {
  ptr pointer = malloc(sizeof(float));
  memcpy(pointer, &val, sizeof(float));
  return pointer;
}
float _store_read_float(ptr pointer) {
  return *((float*) pointer);
}
ptr _store_int(int val) {
  ptr pointer = malloc(sizeof(int));
  memcpy(pointer, &val, sizeof(int));
  return pointer;
}
int _store_read_int(ptr pointer) {
  return *((int*) pointer);
}
void _store_free(ptr pointer) {
  free(pointer);
}