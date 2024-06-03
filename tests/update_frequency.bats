#!/usr/bin/env bats

load setup.bash
load functions.bash

@test "update frequency test slow" {
    prepare_log

    run_slow server freq --sample-window 100 --trace --checksum --server --duration 240 --target $SERVER_POINT
    server=$!
    sleep 1

    run_slow client freq --sample-window 100 --trace --checksum --mean 0.2 --max-dev 0.01 --max-mean-error 0.001 --warmup 100 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}

@test "update frequency test slow nowindow" {
    prepare_log

    run_slow server freq --sample-window 1 --trace --checksum --server --duration 240 --target $SERVER_POINT
    server=$!
    sleep 1

    run_slow client freq --sample-window 1 --trace --checksum --mean 0.2 --max-dev 1 --max-mean-error 1 --warmup 100 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}

@test "update frequency test fast" {
    prepare_log

    run_fast server freq --sample-window 0 --trace --checksum --server --duration 240 --target $SERVER_POINT
    server=$!
    sleep 1

    run_fast client freq --sample-window 100 --trace --checksum --mean 0.05 --max-dev 0.01 --max-mean-error 0.001 --warmup 100 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}

@test "update frequency test fast nowindow" {
    prepare_log

    run_fast server freq --sample-window 1 --trace --checksum --server --duration 240 --target $SERVER_POINT
    server=$!
    sleep 1

    run_fast client freq --sample-window 1 --trace --checksum --mean 0.05 --max-dev 1 --max-mean-error 1 --warmup 100 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}
