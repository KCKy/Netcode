prepare_log()
{
    logdir="$BATS_TEST_DIRNAME/${BATS_TEST_NAME}_${BATS_TEST_NUMBER}"
    if [ -d "$logdir" ]; then rm -rf "$logdir"; fi
}

run_base()
{
    tag=$1
    shift 1
    dotnet "$TESTER" --comlog "$logdir/${tag}_com.log" --log "$logdir/${tag}_test.log" --gamelog "$logdir/${tag}_game.log" "$@" &
}

run_slow()
{
    run_base "$@" --tickrate 5 --delta 0.05
}

run_fast()
{
    run_base "$@" --tickrate 20 --delta 0.05
}

run_slow_1()
{
    run_slow "$@" --sample-window 100 --trace --checksum
}

run_slow_2()
{
    run_slow "$@" --sample-window 100 --trace
}

run_slow_3()
{
    run_slow "$@" --sample-window 100 --checksum
}

run_slow_4()
{
    run_slow "$@" --sample-window 100
}

run_fast_1()
{
    run_fast "$@" --sample-window 100 --trace --checksum
}

run_fast_2()
{
    run_fast "$@" --sample-window 100 --trace
}

run_fast_3()
{
    run_fast "$@" --sample-window 100 --checksum
}

run_fast_4()
{
    run_fast "$@" --sample-window 100
}
